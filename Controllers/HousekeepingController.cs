using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HotelManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin,Manager")]
public class HousekeepingController : ControllerBase
{
    private readonly IHousekeepingService _housekeepingService;

    public HousekeepingController(IHousekeepingService housekeepingService)
    {
        _housekeepingService = housekeepingService;
    }

    [HttpGet("hotel/{hotelId}")]
    public async Task<IActionResult> GetTasks(int hotelId, [FromQuery] DateTime? date, [FromQuery] string? status)
    {
        var tasks = await _housekeepingService.GetTasksAsync(hotelId, date, status);
        return Ok(tasks);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var task = await _housekeepingService.GetTaskByIdAsync(id);
        if (task == null) return NotFound();
        return Ok(task);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHousekeepingTaskDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var task = await _housekeepingService.CreateTaskAsync(dto, userId);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHousekeepingTaskDto dto)
    {
        var task = await _housekeepingService.UpdateTaskAsync(id, dto);
        return Ok(task);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _housekeepingService.DeleteTaskAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartTask(int id)
    {
        var task = await _housekeepingService.StartTaskAsync(id);
        return Ok(task);
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteTask(int id)
    {
        var task = await _housekeepingService.CompleteTaskAsync(id);
        return Ok(task);
    }

    [HttpGet("hotel/{hotelId}/schedule")]
    public async Task<IActionResult> GetSchedule(int hotelId, [FromQuery] DateTime? date)
    {
        var schedule = await _housekeepingService.GetScheduleAsync(hotelId, date ?? DateTime.Today);
        return Ok(schedule);
    }

    [HttpGet("hotel/{hotelId}/performance")]
    public async Task<IActionResult> GetPerformance(int hotelId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var performance = await _housekeepingService.GetPerformanceAsync(
            hotelId,
            from ?? DateTime.Today.AddDays(-30),
            to ?? DateTime.Today);
        return Ok(performance);
    }

    [HttpPost("hotel/{hotelId}/generate-daily")]
    public async Task<IActionResult> GenerateDailyTasks(int hotelId, [FromQuery] DateTime? date)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _housekeepingService.GenerateDailyTasksAsync(hotelId, date ?? DateTime.Today, userId);
        return Ok(new { message = "Daily tasks generated successfully" });
    }
}
