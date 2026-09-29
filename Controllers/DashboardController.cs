using HotelManagement.Models.Constants;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers;

/// <summary>
/// The front desk's numbers for one hotel. Pass the hotel's own calendar day as `date` (and its
/// offset from UTC) so "today" is the hotel's today; without them the server's UTC day is used.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
public class DashboardController : ControllerBase
{
    private const int MaxTrendDays = 31;
    // Real offsets run from UTC−12 to UTC+14
    private const int MaxOffsetMinutes = 14 * 60;

    private readonly IDashboardService _dashboard;
    private readonly IHotelAccessService _hotelAccess;

    public DashboardController(IDashboardService dashboard, IHotelAccessService hotelAccess)
    {
        _dashboard = dashboard;
        _hotelAccess = hotelAccess;
    }

    [HttpGet("today")]
    public async Task<IActionResult> Today([FromQuery] int hotelId, [FromQuery] DateTime? date, [FromQuery] int utcOffsetMinutes = 0)
    {
        if (Math.Abs(utcOffsetMinutes) > MaxOffsetMinutes)
            return BadRequest(new { message = "utcOffsetMinutes must be between -840 and 840" });
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _dashboard.GetTodayAsync(hotelId, date ?? DateTime.UtcNow.Date, utcOffsetMinutes));
    }

    [HttpGet("trend")]
    public async Task<IActionResult> Trend([FromQuery] int hotelId, [FromQuery] DateTime? date, [FromQuery] int days = 7, [FromQuery] int utcOffsetMinutes = 0)
    {
        if (days < 1 || days > MaxTrendDays)
            return BadRequest(new { message = $"days must be between 1 and {MaxTrendDays}" });
        if (Math.Abs(utcOffsetMinutes) > MaxOffsetMinutes)
            return BadRequest(new { message = "utcOffsetMinutes must be between -840 and 840" });
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _dashboard.GetTrendAsync(hotelId, date ?? DateTime.UtcNow.Date, days, utcOffsetMinutes));
    }
}
