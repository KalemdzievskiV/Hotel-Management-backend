using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers;

/// <summary>
/// Controller for managing hotel guests
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GuestsController : CrudController<GuestDto>
{
    private readonly IGuestService _guestService;

    public GuestsController(IGuestService service) : base(service)
    {
        _guestService = service;
    }

    /// <summary>
    /// Get all guests accessible to current user
    /// (Walk-in guests they created + all registered users)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public override async Task<IActionResult> GetAllAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();
        
        var guests = await _guestService.GetMyAccessibleGuestsAsync(userId);
        return Ok(guests);
    }
    
    /// <summary>
    /// Get all guests (SuperAdmin only - no filtering)
    /// </summary>
    [HttpGet("all-unfiltered")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> GetAllUnfilteredAsync()
    {
        return Ok(await _guestService.GetAllAsync());
    }

    /// <summary>
    /// Get guest by ID
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public override async Task<IActionResult> GetByIdAsync(int id)
    {
        var guest = await _guestService.GetByIdAsync(id);
        return guest == null ? NotFound() : Ok(guest);
    }

    /// <summary>
    /// Create a new guest (Admin/Manager only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public override async Task<IActionResult> CreateAsync([FromBody] GuestDto dto)
    {
        var created = await _guestService.CreateAsync(dto);
        return CreatedAtAction("GetById", new { id = created.Id }, created);
    }

    /// <summary>
    /// Update a guest (Admin/Manager only)
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public override async Task<IActionResult> UpdateAsync(int id, [FromBody] GuestDto dto)
    {
        var updated = await _guestService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    /// <summary>
    /// Delete a guest (Admin only)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
    public override async Task<IActionResult> DeleteAsync(int id)
    {
        await _guestService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Search guests by name
    /// </summary>
    [HttpGet("search")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> SearchByNameAsync([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Search term cannot be empty");

        var guests = await _guestService.SearchByNameAsync(name);
        return Ok(guests);
    }

    /// <summary>
    /// Get guest by email
    /// </summary>
    [HttpGet("email/{email}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetByEmailAsync(string email)
    {
        var guest = await _guestService.GetByEmailAsync(email);
        return guest == null ? NotFound() : Ok(guest);
    }

    /// <summary>
    /// Get guest by phone number
    /// </summary>
    [HttpGet("phone/{phoneNumber}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetByPhoneNumberAsync(string phoneNumber)
    {
        var guest = await _guestService.GetByPhoneNumberAsync(phoneNumber);
        return guest == null ? NotFound() : Ok(guest);
    }

    /// <summary>
    /// Get all VIP guests
    /// </summary>
    [HttpGet("vip")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetVIPGuestsAsync()
    {
        var guests = await _guestService.GetVIPGuestsAsync();
        return Ok(guests);
    }

    /// <summary>
    /// Get all active guests (not blacklisted)
    /// </summary>
    [HttpGet("active")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetActiveGuestsAsync()
    {
        var guests = await _guestService.GetActiveGuestsAsync();
        return Ok(guests);
    }

    /// <summary>
    /// Get blacklisted guests (Admin only)
    /// </summary>
    [HttpGet("blacklisted")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
    public async Task<IActionResult> GetBlacklistedGuestsAsync()
    {
        var guests = await _guestService.GetBlacklistedGuestsAsync();
        return Ok(guests);
    }

    /// <summary>
    /// Blacklist a guest (Admin only)
    /// </summary>
    [HttpPost("{id:int}/blacklist")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
    public async Task<IActionResult> BlacklistGuestAsync(int id, [FromBody] BlacklistRequest request)
    {
        await _guestService.BlacklistGuestAsync(id, request.Reason);
        return Ok(new { message = "Guest blacklisted successfully" });
    }

    /// <summary>
    /// Remove guest from blacklist (Admin only)
    /// </summary>
    [HttpPost("{id:int}/unblacklist")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
    public async Task<IActionResult> UnblacklistGuestAsync(int id)
    {
        await _guestService.UnblacklistGuestAsync(id);
        return Ok(new { message = "Guest removed from blacklist" });
    }

    /// <summary>
    /// Set VIP status (Admin/Manager only)
    /// </summary>
    [HttpPatch("{id:int}/vip")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> SetVIPStatusAsync(int id, [FromBody] VIPStatusRequest request)
    {
        await _guestService.SetVIPStatusAsync(id, request.IsVIP);
        return Ok(new { message = $"Guest VIP status updated to {request.IsVIP}" });
    }
    
    /// <summary>
    /// Get all walk-in guests for a specific hotel
    /// </summary>
    [HttpGet("hotel/{hotelId:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetGuestsByHotelAsync(int hotelId)
    {
        var guests = await _guestService.GetGuestsByHotelIdAsync(hotelId);
        return Ok(guests);
    }
    
    /// <summary>
    /// Get all walk-in guests created by current user
    /// </summary>
    [HttpGet("my-guests")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetMyGuestsAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();
        
        var guests = await _guestService.GetGuestsCreatedByUserAsync(userId);
        return Ok(guests);
    }
}

/// <summary>
/// DTO for blacklisting a guest
/// </summary>
public class BlacklistRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO for updating VIP status
/// </summary>
public class VIPStatusRequest
{
    public bool IsVIP { get; set; }
}
