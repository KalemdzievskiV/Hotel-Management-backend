using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

public class HousekeepingService : IHousekeepingService
{
    private readonly ApplicationDbContext _context;

    public HousekeepingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<HousekeepingTaskDto>> GetTasksAsync(int hotelId, DateTime? date, string? status)
    {
        var query = _context.HousekeepingTasks
            .Include(t => t.Room).ThenInclude(r => r.Hotel)
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Where(t => t.Room.HotelId == hotelId);

        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(t => t.ScheduledFor.Date == day);
        }

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<HousekeepingTaskStatus>(status, true, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);

        return await query
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.ScheduledFor)
            .Select(t => MapToDto(t))
            .ToListAsync();
    }

    public async Task<HousekeepingTaskDto?> GetTaskByIdAsync(int id)
    {
        var task = await _context.HousekeepingTasks
            .Include(t => t.Room).ThenInclude(r => r.Hotel)
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id);

        return task == null ? null : MapToDto(task);
    }

    public async Task<HousekeepingTaskDto> CreateTaskAsync(CreateHousekeepingTaskDto dto, string userId)
    {
        var task = new HousekeepingTask
        {
            RoomId = dto.RoomId,
            AssignedToUserId = dto.AssignedToUserId,
            CreatedByUserId = userId,
            Type = dto.Type,
            Priority = dto.Priority,
            Status = HousekeepingTaskStatus.Pending,
            ScheduledFor = dto.ScheduledFor,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.HousekeepingTasks.Add(task);
        await _context.SaveChangesAsync();

        return await GetTaskByIdAsync(task.Id) ?? throw new InvalidOperationException("Failed to retrieve created task");
    }

    public async Task<HousekeepingTaskDto> UpdateTaskAsync(int id, UpdateHousekeepingTaskDto dto)
    {
        var task = await _context.HousekeepingTasks.FindAsync(id)
            ?? throw new KeyNotFoundException($"Task {id} not found");

        if (dto.AssignedToUserId != null) task.AssignedToUserId = string.IsNullOrEmpty(dto.AssignedToUserId) ? null : dto.AssignedToUserId;
        if (dto.Priority.HasValue) task.Priority = dto.Priority.Value;
        if (dto.Status.HasValue) task.Status = dto.Status.Value;
        if (dto.ScheduledFor.HasValue) task.ScheduledFor = dto.ScheduledFor.Value;
        if (dto.Notes != null) task.Notes = dto.Notes;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetTaskByIdAsync(id) ?? throw new InvalidOperationException("Task not found after update");
    }

    public async Task DeleteTaskAsync(int id)
    {
        var task = await _context.HousekeepingTasks.FindAsync(id)
            ?? throw new KeyNotFoundException($"Task {id} not found");

        _context.HousekeepingTasks.Remove(task);
        await _context.SaveChangesAsync();
    }

    public async Task<HousekeepingTaskDto> StartTaskAsync(int id)
    {
        var task = await _context.HousekeepingTasks.FindAsync(id)
            ?? throw new KeyNotFoundException($"Task {id} not found");

        task.Status = HousekeepingTaskStatus.InProgress;
        task.StartedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetTaskByIdAsync(id) ?? throw new InvalidOperationException();
    }

    public async Task<HousekeepingTaskDto> CompleteTaskAsync(int id)
    {
        var task = await _context.HousekeepingTasks
            .Include(t => t.Room)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException($"Task {id} not found");

        task.Status = HousekeepingTaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        if (!task.StartedAt.HasValue) task.StartedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        // Update room's LastCleaned if it's a cleaning task
        if (task.Type == HousekeepingTaskType.CleanRoom || task.Type == HousekeepingTaskType.DeepClean)
        {
            var room = task.Room;
            room.LastCleaned = DateTime.UtcNow;
            room.Status = RoomStatus.Available;
        }

        await _context.SaveChangesAsync();
        return await GetTaskByIdAsync(id) ?? throw new InvalidOperationException();
    }

    public async Task<HousekeepingScheduleDto> GetScheduleAsync(int hotelId, DateTime date)
    {
        var day = date.Date;
        var tasks = await _context.HousekeepingTasks
            .Include(t => t.Room).ThenInclude(r => r.Hotel)
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Where(t => t.Room.HotelId == hotelId && t.ScheduledFor.Date == day)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.Room.RoomNumber)
            .ToListAsync();

        return new HousekeepingScheduleDto
        {
            Date = day,
            TotalTasks = tasks.Count,
            PendingTasks = tasks.Count(t => t.Status == HousekeepingTaskStatus.Pending),
            InProgressTasks = tasks.Count(t => t.Status == HousekeepingTaskStatus.InProgress),
            CompletedTasks = tasks.Count(t => t.Status == HousekeepingTaskStatus.Completed),
            Tasks = tasks.Select(MapToDto).ToList()
        };
    }

    public async Task<IEnumerable<HousekeeperPerformanceDto>> GetPerformanceAsync(int hotelId, DateTime from, DateTime to)
    {
        var tasks = await _context.HousekeepingTasks
            .Include(t => t.Room)
            .Include(t => t.AssignedTo)
            .Where(t => t.Room.HotelId == hotelId
                && t.AssignedToUserId != null
                && t.ScheduledFor >= from
                && t.ScheduledFor <= to)
            .ToListAsync();

        return tasks
            .GroupBy(t => t.AssignedToUserId!)
            .Select(g =>
            {
                var completed = g.Where(t => t.Status == HousekeepingTaskStatus.Completed).ToList();
                var durations = completed
                    .Where(t => t.StartedAt.HasValue && t.CompletedAt.HasValue)
                    .Select(t => (t.CompletedAt!.Value - t.StartedAt!.Value).TotalMinutes)
                    .ToList();

                var user = g.First().AssignedTo;
                return new HousekeeperPerformanceDto
                {
                    UserId = g.Key,
                    Name = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                    TasksAssigned = g.Count(),
                    TasksCompleted = completed.Count,
                    AverageDurationMinutes = durations.Count > 0 ? Math.Round(durations.Average(), 1) : null
                };
            })
            .OrderByDescending(p => p.TasksCompleted)
            .ToList();
    }

    public async Task GenerateDailyTasksAsync(int hotelId, DateTime date, string userId)
    {
        var day = date.Date;

        // Find rooms that had checkouts today
        var checkoutRoomIds = await _context.Reservations
            .Where(r => r.HotelId == hotelId
                && r.CheckOutDate.Date == day
                && (r.Status == ReservationStatus.CheckedOut || r.Status == ReservationStatus.Confirmed))
            .Select(r => r.RoomId)
            .Distinct()
            .ToListAsync();

        var existingTaskRoomIds = await _context.HousekeepingTasks
            .Where(t => t.Room.HotelId == hotelId && t.ScheduledFor.Date == day)
            .Select(t => t.RoomId)
            .ToListAsync();

        var newTaskRoomIds = checkoutRoomIds.Except(existingTaskRoomIds).ToList();

        var tasks = newTaskRoomIds.Select(roomId => new HousekeepingTask
        {
            RoomId = roomId,
            CreatedByUserId = userId,
            Type = HousekeepingTaskType.CleanRoom,
            Priority = HousekeepingTaskPriority.High,
            Status = HousekeepingTaskStatus.Pending,
            ScheduledFor = day,
            Notes = "Auto-generated from checkout",
            CreatedAt = DateTime.UtcNow
        });

        _context.HousekeepingTasks.AddRange(tasks);
        await _context.SaveChangesAsync();
    }

    private static HousekeepingTaskDto MapToDto(HousekeepingTask t) => new()
    {
        Id = t.Id,
        RoomId = t.RoomId,
        RoomNumber = t.Room?.RoomNumber ?? string.Empty,
        HotelId = t.Room?.HotelId ?? 0,
        HotelName = t.Room?.Hotel?.Name ?? string.Empty,
        AssignedToUserId = t.AssignedToUserId,
        AssignedToName = t.AssignedTo != null ? $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}" : null,
        CreatedByUserId = t.CreatedByUserId,
        CreatedByName = t.CreatedBy != null ? $"{t.CreatedBy.FirstName} {t.CreatedBy.LastName}" : string.Empty,
        Type = t.Type,
        Priority = t.Priority,
        Status = t.Status,
        ScheduledFor = t.ScheduledFor,
        StartedAt = t.StartedAt,
        CompletedAt = t.CompletedAt,
        Notes = t.Notes,
        CreatedAt = t.CreatedAt
    };
}
