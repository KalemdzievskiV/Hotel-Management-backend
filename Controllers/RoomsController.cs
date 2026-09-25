using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers;

/// <summary>
/// Rooms. Read endpoints used for booking (by hotel, availability, by id) are open to any
/// authenticated user; everything operational is limited to staff of the room's hotel.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoomsController : CrudController<RoomDto>
{
    private const string ManagementRoles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}";
    private const string StaffRoles = $"{ManagementRoles},{AppRoles.Housekeeper}";

    private readonly IRoomService _roomService;
    private readonly IReservationService _reservationService;
    private readonly IHotelAccessService _hotelAccess;
    private readonly IEntitlementService _entitlements;

    public RoomsController(
        IRoomService service,
        IReservationService reservationService,
        IHotelAccessService hotelAccess,
        IEntitlementService entitlements) : base(service)
    {
        _entitlements = entitlements;
        _roomService = service;
        _reservationService = reservationService;
        _hotelAccess = hotelAccess;
    }

    private async Task<bool> CanAccessRoomAsync(int roomId)
    {
        var hotelId = await _hotelAccess.GetRoomHotelIdAsync(roomId);
        return hotelId.HasValue && await _hotelAccess.CanAccessHotelAsync(hotelId.Value);
    }

    private async Task<List<RoomDto>> GetAccessibleRoomsAsync()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return (await _roomService.GetRoomsForHotelsAsync(hotelIds)).ToList();
    }

    /// <summary>
    /// Rooms across all hotels the user can access
    /// </summary>
    [HttpGet]
    [Authorize(Roles = StaffRoles)]
    public override async Task<IActionResult> GetAllAsync()
    {
        return Ok(await GetAccessibleRoomsAsync());
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public override async Task<IActionResult> GetByIdAsync(int id)
    {
        var room = await _roomService.GetByIdAsync(id);
        return room == null ? NotFound() : Ok(room);
    }

    [HttpGet("hotel/{hotelId:int}")]
    [Authorize]
    public async Task<IActionResult> GetRoomsByHotelAsync(int hotelId)
    {
        return Ok(await _roomService.GetRoomsByHotelIdAsync(hotelId));
    }

    [HttpGet("hotel/{hotelId:int}/available")]
    [Authorize]
    public async Task<IActionResult> GetAvailableRoomsAsync(int hotelId)
    {
        return Ok(await _roomService.GetAvailableRoomsByHotelAsync(hotelId));
    }

    [HttpGet("hotel/{hotelId:int}/short-stay")]
    [Authorize]
    public async Task<IActionResult> GetShortStayRoomsAsync(int hotelId)
    {
        var allRooms = await _roomService.GetRoomsByHotelIdAsync(hotelId);
        return Ok(allRooms.Where(r => r.AllowsShortStay && r.IsActive).ToList());
    }

    [HttpGet("hotel/{hotelId:int}/status/{status}")]
    [Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> GetRoomsByStatusAsync(int hotelId, RoomStatus status)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _roomService.GetRoomsByHotelAndStatusAsync(hotelId, status));
    }

    [HttpPost]
    [Authorize(Roles = ManagementRoles)]
    public override async Task<IActionResult> CreateAsync([FromBody] RoomDto dto)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(dto.HotelId))
            return Forbid();

        await _entitlements.EnsureCanAddRoomAsync(dto.HotelId);
        var created = await _roomService.CreateAsync(dto);
        return CreatedAtAction("GetById", new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = ManagementRoles)]
    public override async Task<IActionResult> UpdateAsync(int id, [FromBody] RoomDto dto)
    {
        if (!await CanAccessRoomAsync(id))
            return Forbid();

        return Ok(await _roomService.UpdateAsync(id, dto));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
    public override async Task<IActionResult> DeleteAsync(int id)
    {
        if (!await CanAccessRoomAsync(id))
            return Forbid();

        await _roomService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> UpdateRoomStatusAsync(int id, [FromBody] RoomStatusUpdateDto statusDto)
    {
        if (!await CanAccessRoomAsync(id))
            return Forbid();

        return Ok(await _roomService.UpdateRoomStatusAsync(id, statusDto.Status));
    }

    [HttpPost("{id:int}/clean")]
    [Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> MarkAsCleanedAsync(int id)
    {
        if (!await CanAccessRoomAsync(id))
            return Forbid();

        await _roomService.MarkRoomAsCleanedAsync(id);
        return Ok(new { message = "Room marked as cleaned" });
    }

    [HttpPost("{id:int}/maintenance")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> RecordMaintenanceAsync(int id, [FromBody] MaintenanceDto maintenanceDto)
    {
        if (!await CanAccessRoomAsync(id))
            return Forbid();

        await _roomService.RecordMaintenanceAsync(id, maintenanceDto.Notes);
        return Ok(new { message = "Maintenance recorded" });
    }

    [HttpGet("stats/status-summary")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetRoomStatusSummary()
    {
        var rooms = await GetAccessibleRoomsAsync();

        var statusSummary = rooms
            .GroupBy(r => r.Status)
            .Select(g => new
            {
                Status = g.Key,
                StatusName = g.Key.ToString(),
                Count = g.Count()
            })
            .OrderBy(x => x.Status)
            .ToList();

        return Ok(new
        {
            TotalRooms = rooms.Count,
            StatusBreakdown = statusSummary
        });
    }

    [HttpGet("stats/occupancy-rate")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetOccupancyRate()
    {
        var rooms = await GetAccessibleRoomsAsync();

        var totalRooms = rooms.Count;
        var occupiedRooms = rooms.Count(r => r.Status == RoomStatus.Occupied);
        var reservedRooms = rooms.Count(r => r.Status == RoomStatus.Reserved);
        // Occupancy includes both Occupied and Reserved rooms
        var effectivelyOccupied = occupiedRooms + reservedRooms;
        var occupancyRate = totalRooms > 0 ? (double)effectivelyOccupied / totalRooms * 100 : 0;

        return Ok(new
        {
            TotalRooms = totalRooms,
            OccupiedRooms = occupiedRooms,
            ReservedRooms = reservedRooms,
            EffectivelyOccupied = effectivelyOccupied,
            AvailableRooms = rooms.Count(r => r.Status == RoomStatus.Available),
            OccupancyRate = Math.Round(occupancyRate, 1)
        });
    }

    [HttpGet("stats/occupancy-trends")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetOccupancyTrends([FromQuery] int days = 30)
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        var totalRooms = (await _roomService.GetRoomsForHotelsAsync(hotelIds)).Count();

        if (totalRooms == 0)
        {
            return Ok(new
            {
                CurrentOccupancy = 0,
                ThisMonthAverage = 0,
                LastMonthAverage = 0,
                DailyOccupancy = new List<object>()
            });
        }

        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-days);
        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var rangeStart = startDate < lastMonthStart ? startDate : lastMonthStart;

        // Only reservations overlapping the period we report on
        var reservations = (await _reservationService.GetReservationsByDateRangeAsync(rangeStart, today.AddDays(1), hotelIds))
            .ToList();

        int OccupiedOn(DateTime date, bool includeCheckedOut) =>
            reservations.Count(r =>
                r.CheckInDate.Date <= date &&
                r.CheckOutDate.Date > date &&
                (r.Status == ReservationStatus.Confirmed ||
                 r.Status == ReservationStatus.CheckedIn ||
                 (includeCheckedOut && r.Status == ReservationStatus.CheckedOut)));

        double Rate(int occupied) => (double)occupied / totalRooms * 100;

        var dailyOccupancy = new List<(DateTime Date, int Occupied)>();
        for (var date = startDate; date <= today; date = date.AddDays(1))
            dailyOccupancy.Add((date, OccupiedOn(date, includeCheckedOut: true)));

        var thisMonthRates = dailyOccupancy.Where(d => d.Date >= thisMonthStart).Select(d => Rate(d.Occupied)).ToList();

        var lastMonthRates = new List<double>();
        for (var date = lastMonthStart; date < thisMonthStart; date = date.AddDays(1))
            lastMonthRates.Add(Rate(OccupiedOn(date, includeCheckedOut: true)));

        return Ok(new
        {
            CurrentOccupancy = Math.Round(Rate(OccupiedOn(today, includeCheckedOut: false)), 1),
            ThisMonthAverage = Math.Round(thisMonthRates.Count > 0 ? thisMonthRates.Average() : 0, 1),
            LastMonthAverage = Math.Round(lastMonthRates.Average(), 1),
            TotalRooms = totalRooms,
            DailyOccupancy = dailyOccupancy.Select(d => new
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                OccupiedRooms = d.Occupied,
                TotalRooms = totalRooms,
                OccupancyRate = Math.Round(Rate(d.Occupied), 1)
            })
        });
    }
}

public class RoomStatusUpdateDto
{
    public RoomStatus Status { get; set; }
}

public class MaintenanceDto
{
    public string Notes { get; set; } = string.Empty;
}
