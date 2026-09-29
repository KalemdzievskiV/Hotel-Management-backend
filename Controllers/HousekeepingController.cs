using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HotelManagement.Controllers;

/// <summary>
/// Housekeeping tasks. Housekeepers can view and work their hotel's tasks;
/// planning (create/edit/delete/generate) and performance reports are for management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = StaffRoles)]
public class HousekeepingController : ControllerBase
{
    private const string ManagementRoles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}";
    private const string StaffRoles = $"{ManagementRoles},{AppRoles.Housekeeper}";

    private readonly IHousekeepingService _housekeepingService;
    private readonly IHotelAccessService _hotelAccess;
    private readonly INotificationService _notifications;

    public HousekeepingController(
        IHousekeepingService housekeepingService,
        IHotelAccessService hotelAccess,
        INotificationService notifications)
    {
        _housekeepingService = housekeepingService;
        _hotelAccess = hotelAccess;
        _notifications = notifications;
    }

    private async Task<bool> CanAccessTaskAsync(int taskId)
    {
        var task = await _housekeepingService.GetTaskByIdAsync(taskId);
        return task != null && await _hotelAccess.CanAccessHotelAsync(task.HotelId);
    }

    [HttpGet("hotel/{hotelId}")]
    public async Task<IActionResult> GetTasks(int hotelId, [FromQuery] DateTime? date, [FromQuery] string? status)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _housekeepingService.GetTasksAsync(hotelId, date, status));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var task = await _housekeepingService.GetTaskByIdAsync(id);
        if (task == null || !await _hotelAccess.CanAccessHotelAsync(task.HotelId))
            return NotFound();

        return Ok(task);
    }

    [HttpPost]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> Create([FromBody] CreateHousekeepingTaskDto dto)
    {
        var roomHotelId = await _hotelAccess.GetRoomHotelIdAsync(dto.RoomId);
        if (!roomHotelId.HasValue || !await _hotelAccess.CanAccessHotelAsync(roomHotelId.Value))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var task = await _housekeepingService.CreateTaskAsync(dto, userId);
        await _notifications.TaskAssignedAsync(task);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHousekeepingTaskDto dto)
    {
        var before = await _housekeepingService.GetTaskByIdAsync(id);
        if (before == null || !await _hotelAccess.CanAccessHotelAsync(before.HotelId))
            return NotFound();

        var task = await _housekeepingService.UpdateTaskAsync(id, dto);

        // Tell whoever has the task now if it's new to them, or if it just became urgent
        if (task.AssignedToUserId != null && task.AssignedToUserId != before.AssignedToUserId)
            await _notifications.TaskAssignedAsync(task);
        else if (task.Priority == HousekeepingTaskPriority.Urgent && before.Priority != HousekeepingTaskPriority.Urgent)
            await _notifications.TaskBecameUrgentAsync(task);

        return Ok(task);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await CanAccessTaskAsync(id))
            return NotFound();

        await _housekeepingService.DeleteTaskAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartTask(int id)
    {
        if (!await CanAccessTaskAsync(id))
            return NotFound();

        return Ok(await _housekeepingService.StartTaskAsync(id));
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteTask(int id)
    {
        if (!await CanAccessTaskAsync(id))
            return NotFound();

        return Ok(await _housekeepingService.CompleteTaskAsync(id));
    }

    [HttpGet("hotel/{hotelId}/schedule")]
    public async Task<IActionResult> GetSchedule(int hotelId, [FromQuery] DateTime? date)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _housekeepingService.GetScheduleAsync(hotelId, date ?? DateTime.Today));
    }

    [HttpGet("hotel/{hotelId}/performance")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetPerformance(int hotelId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _housekeepingService.GetPerformanceAsync(
            hotelId,
            from ?? DateTime.Today.AddDays(-30),
            to ?? DateTime.Today));
    }

    [HttpPost("hotel/{hotelId}/generate-daily")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GenerateDailyTasks(int hotelId, [FromQuery] DateTime? date)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _housekeepingService.GenerateDailyTasksAsync(hotelId, date ?? DateTime.Today, userId);
        return Ok(new { message = "Daily tasks generated successfully" });
    }
}
