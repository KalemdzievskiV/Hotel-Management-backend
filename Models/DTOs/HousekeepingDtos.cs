using System.ComponentModel.DataAnnotations;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

public class HousekeepingTaskDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public HousekeepingTaskType Type { get; set; }
    public string TypeName => Type.ToString();
    public HousekeepingTaskPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public HousekeepingTaskStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime ScheduledFor { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? DurationMinutes => StartedAt.HasValue && CompletedAt.HasValue
        ? (int)(CompletedAt.Value - StartedAt.Value).TotalMinutes
        : null;
}

public class CreateHousekeepingTaskDto
{
    [Required]
    public int RoomId { get; set; }

    public string? AssignedToUserId { get; set; }

    [Required]
    public HousekeepingTaskType Type { get; set; }

    public HousekeepingTaskPriority Priority { get; set; } = HousekeepingTaskPriority.Normal;

    public DateTime ScheduledFor { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateHousekeepingTaskDto
{
    public string? AssignedToUserId { get; set; }
    public HousekeepingTaskPriority? Priority { get; set; }
    public HousekeepingTaskStatus? Status { get; set; }
    public DateTime? ScheduledFor { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class HousekeepingScheduleDto
{
    public DateTime Date { get; set; }
    public int TotalTasks { get; set; }
    public int PendingTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int CompletedTasks { get; set; }
    public List<HousekeepingTaskDto> Tasks { get; set; } = new();
}

public class HousekeeperPerformanceDto
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TasksCompleted { get; set; }
    public int TasksAssigned { get; set; }
    public double CompletionRate => TasksAssigned > 0 ? Math.Round((double)TasksCompleted / TasksAssigned * 100, 1) : 0;
    public double? AverageDurationMinutes { get; set; }
}
