using System.Security.Claims;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
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

    public RoomsController(IRoomService service, IHotelService hotelService) : base(service)
    {
        _roomService = service;
        _hotelService = hotelService;
    }

    /// <summary>
    /// Get all rooms (SuperAdmin/Admin/Manager only)
    /// Filters by user's hotels for non-SuperAdmin users
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public override async Task<IActionResult> GetAllAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        
        if (isSuperAdmin)
        {
            // SuperAdmin sees all rooms
            return Ok(await _roomService.GetAllAsync());
        }
        
        // Regular admin/manager sees only rooms from their hotels
        var userHotels = await _hotelService.GetAllHotelsForUserAsync(userId!, isSuperAdmin: false);
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
    /// Create a new room (Admin/Manager only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public override async Task<IActionResult> CreateAsync([FromBody] RoomDto dto)
    {
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
