using System.Security.Claims;
using HotelManagement.Authorization.Requirements;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers;

/// <summary>
/// Controller for managing hotel rooms
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoomsController : CrudController<RoomDto>
{
    private readonly IRoomService _roomService;
    private readonly IHotelService _hotelService;
    private readonly IReservationService _reservationService;
    private readonly IAuthorizationService _authorizationService;

    public RoomsController(
        IRoomService service, 
        IHotelService hotelService, 
        IReservationService reservationService,
        IAuthorizationService authorizationService) : base(service)
    {
        _roomService = service;
        _hotelService = hotelService;
        _reservationService = reservationService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Get all rooms (filtered by user's hotels)
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "ManagerOrAbove")]
    public override async Task<IActionResult> GetAllAsync()
    {
        var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        
        if (isSuperAdmin)
        {
            // SuperAdmin sees all rooms
            return Ok(await _roomService.GetAllAsync());
        }
        
        // Admin/Manager sees only rooms from their hotels (service layer filters hotels)
        var userHotels = await _hotelService.GetAllAsync();
        var hotelIds = userHotels.Select(h => h.Id).ToList();
        
        var allRooms = await _roomService.GetAllAsync();
        var filteredRooms = allRooms.Where(r => hotelIds.Contains(r.HotelId));
        
        return Ok(filteredRooms);
    }

    /// <summary>
    /// Get room by ID
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize]
    public override async Task<IActionResult> GetByIdAsync(int id)
    {
        var room = await _roomService.GetByIdAsync(id);
        return room == null ? NotFound() : Ok(room);
    }

    /// <summary>
    /// Get all rooms for a specific hotel
    /// </summary>
    [HttpGet("hotel/{hotelId:int}")]
    [Authorize]
    public async Task<IActionResult> GetRoomsByHotelAsync(int hotelId)
    {
        var rooms = await _roomService.GetRoomsByHotelIdAsync(hotelId);
        return Ok(rooms);
    }

    /// <summary>
    /// Get available rooms for a specific hotel
    /// </summary>
    [HttpGet("hotel/{hotelId:int}/available")]
    [Authorize]
    public async Task<IActionResult> GetAvailableRoomsAsync(int hotelId)
    {
        var rooms = await _roomService.GetAvailableRoomsByHotelAsync(hotelId);
        return Ok(rooms);
    }

    /// <summary>
    /// Get short-stay enabled rooms for a specific hotel
    /// </summary>
    [HttpGet("hotel/{hotelId:int}/short-stay")]
    [Authorize]
    public async Task<IActionResult> GetShortStayRoomsAsync(int hotelId)
    {
        var allRooms = await _roomService.GetRoomsByHotelIdAsync(hotelId);
        var shortStayRooms = allRooms.Where(r => r.AllowsShortStay && r.IsActive).ToList();
        return Ok(shortStayRooms);
    }

    /// <summary>
    /// Get rooms by hotel and status
    /// </summary>
    [HttpGet("hotel/{hotelId:int}/status/{status}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Housekeeper}")]
    public async Task<IActionResult> GetRoomsByStatusAsync(int hotelId, RoomStatus status)
    {
        var rooms = await _roomService.GetRoomsByHotelAndStatusAsync(hotelId, status);
        return Ok(rooms);
    }

    /// <summary>
    /// Create a new room (user must own the hotel)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "ManagerOrAbove")]
    public override async Task<IActionResult> CreateAsync([FromBody] RoomDto dto)
    {
        // Check if user has access to this hotel
        var hotel = await _hotelService.GetByIdAsync(dto.HotelId);
        if (hotel == null)
        {
            return NotFound(new { message = "Hotel not found or you don't have access" });
        }
        
        // Authorization is implicitly checked - if hotel is returned from service, user owns it
        var created = await _roomService.CreateAsync(dto);
        return CreatedAtAction("GetById", new { id = created.Id }, created);
    }

    /// <summary>
    /// Update a room (Admin/Manager only)
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public override async Task<IActionResult> UpdateAsync(int id, [FromBody] RoomDto dto)
    {
        var updated = await _roomService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>
    /// Delete a room (Admin only)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
    public override async Task<IActionResult> DeleteAsync(int id)
    {
        await _roomService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Update room status (Admin/Manager/Housekeeper)
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Housekeeper}")]
    public async Task<IActionResult> UpdateRoomStatusAsync(int id, [FromBody] RoomStatusUpdateDto statusDto)
    {
        var updated = await _roomService.UpdateRoomStatusAsync(id, statusDto.Status);
        return Ok(updated);
    }

    /// <summary>
    /// Mark room as cleaned (Housekeeper/Manager/Admin)
    /// </summary>
    [HttpPost("{id:int}/clean")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Housekeeper}")]
    public async Task<IActionResult> MarkAsCleanedAsync(int id)
    {
        await _roomService.MarkRoomAsCleanedAsync(id);
        return Ok(new { message = "Room marked as cleaned" });
    }

    /// <summary>
    /// Record maintenance for a room (Admin/Manager)
    /// </summary>
    [HttpPost("{id:int}/maintenance")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> RecordMaintenanceAsync(int id, [FromBody] MaintenanceDto maintenanceDto)
    {
        await _roomService.RecordMaintenanceAsync(id, maintenanceDto.Notes);
        return Ok(new { message = "Maintenance recorded" });
    }

    /// <summary>
    /// Get room status summary (filtered by user's hotels)
    /// </summary>
    [HttpGet("stats/status-summary")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetRoomStatusSummary()
    {
        var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        
        IEnumerable<RoomDto> rooms;
        
        if (isSuperAdmin)
        {
            // SuperAdmin sees all rooms
            rooms = await _roomService.GetAllAsync();
        }
        else
        {
            // Admin/Manager sees only rooms from their hotels
            var userHotels = await _hotelService.GetAllAsync();
            var hotelIds = userHotels.Select(h => h.Id).ToList();
            
            var allRooms = await _roomService.GetAllAsync();
            rooms = allRooms.Where(r => hotelIds.Contains(r.HotelId));
        }

        // Group rooms by status
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

        var totalRooms = rooms.Count();

        return Ok(new
        {
            TotalRooms = totalRooms,
            StatusBreakdown = statusSummary
        });
    }

    /// <summary>
    /// Get current occupancy rate (filtered by user's hotels)
    /// </summary>
    [HttpGet("stats/occupancy-rate")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetOccupancyRate()
    {
        var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        
        IEnumerable<RoomDto> rooms;
        
        if (isSuperAdmin)
        {
            // SuperAdmin sees all rooms
            rooms = await _roomService.GetAllAsync();
        }
        else
        {
            // Admin/Manager sees only rooms from their hotels
            var userHotels = await _hotelService.GetAllAsync();
            var hotelIds = userHotels.Select(h => h.Id).ToList();
            
            var allRooms = await _roomService.GetAllAsync();
            rooms = allRooms.Where(r => hotelIds.Contains(r.HotelId)).ToList();
        }

        var totalRooms = rooms.Count();
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

    /// <summary>
    /// Get occupancy trends (filtered by user's hotels)
    /// </summary>
    [HttpGet("stats/occupancy-trends")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetOccupancyTrends([FromQuery] int days = 30)
    {
        var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        
        // Get user's hotels
        IEnumerable<RoomDto> rooms;
        List<int> hotelIds;
        
        if (isSuperAdmin)
        {
            rooms = await _roomService.GetAllAsync();
            hotelIds = rooms.Select(r => r.HotelId).Distinct().ToList();
        }
        else
        {
            var userHotels = await _hotelService.GetAllAsync();
            hotelIds = userHotels.Select(h => h.Id).ToList();
            
            var allRooms = await _roomService.GetAllAsync();
            rooms = allRooms.Where(r => hotelIds.Contains(r.HotelId)).ToList();
        }

        var totalRooms = rooms.Count();
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

        // Get all reservations for the user's hotels
        var allReservations = await _reservationService.GetAllReservationsAsync();
        var relevantReservations = allReservations
            .Where(r => hotelIds.Contains(r.HotelId))
            .ToList();

        // Calculate daily occupancy for the requested period
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-days);
        var dailyOccupancy = new List<object>();

        for (var date = startDate; date <= today; date = date.AddDays(1))
        {
            var occupiedCount = relevantReservations.Count(r =>
                r.CheckInDate.Date <= date &&
                r.CheckOutDate.Date > date &&
                (r.Status == ReservationStatus.Confirmed || 
                 r.Status == ReservationStatus.CheckedIn || 
                 r.Status == ReservationStatus.CheckedOut)
            );

            var occupancyRate = totalRooms > 0 ? (double)occupiedCount / totalRooms * 100 : 0;

            dailyOccupancy.Add(new
            {
                Date = date.ToString("yyyy-MM-dd"),
                OccupiedRooms = occupiedCount,
                TotalRooms = totalRooms,
                OccupancyRate = Math.Round(occupancyRate, 1)
            });
        }

        // Calculate current occupancy
        var currentOccupied = relevantReservations.Count(r =>
            r.CheckInDate.Date <= today &&
            r.CheckOutDate.Date > today &&
            (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn)
        );
        var currentOccupancy = totalRooms > 0 ? (double)currentOccupied / totalRooms * 100 : 0;

        // Calculate this month's average
        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        var thisMonthData = dailyOccupancy
            .Cast<dynamic>()
            .Where(d => DateTime.Parse(d.Date) >= thisMonthStart)
            .ToList();
        var thisMonthAverage = thisMonthData.Any() 
            ? thisMonthData.Average(d => (double)d.OccupancyRate) 
            : 0;

        // Calculate last month's average
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var lastMonthEnd = thisMonthStart.AddDays(-1);
        var lastMonthOccupancy = new List<double>();

        for (var date = lastMonthStart; date <= lastMonthEnd; date = date.AddDays(1))
        {
            var occupiedCount = relevantReservations.Count(r =>
                r.CheckInDate.Date <= date &&
                r.CheckOutDate.Date > date &&
                (r.Status == ReservationStatus.Confirmed || 
                 r.Status == ReservationStatus.CheckedIn || 
                 r.Status == ReservationStatus.CheckedOut)
            );

            var occupancyRate = totalRooms > 0 ? (double)occupiedCount / totalRooms * 100 : 0;
            lastMonthOccupancy.Add(occupancyRate);
        }

        var lastMonthAverage = lastMonthOccupancy.Any() ? lastMonthOccupancy.Average() : 0;

        return Ok(new
        {
            CurrentOccupancy = Math.Round(currentOccupancy, 1),
            ThisMonthAverage = Math.Round(thisMonthAverage, 1),
            LastMonthAverage = Math.Round(lastMonthAverage, 1),
            TotalRooms = totalRooms,
            DailyOccupancy = dailyOccupancy
        });
    }
}

/// <summary>
/// DTO for updating room status
/// </summary>
public class RoomStatusUpdateDto
{
    public RoomStatus Status { get; set; }
}

/// <summary>
/// DTO for recording maintenance
/// </summary>
public class MaintenanceDto
{
    public string Notes { get; set; } = string.Empty;
}
