using System.Security.Claims;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers;

/// <summary>
/// Guest records. Staff only see guests belonging to their hotels (walk-ins created there
/// or guests with a reservation there); see GuestQueries.VisibleToHotels.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GuestsController : CrudController<GuestDto>
{
    private const string ManagementRoles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}";
    private const string AdminRoles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}";

    private readonly IGuestService _guestService;
    private readonly IHotelAccessService _hotelAccess;

    public GuestsController(IGuestService service, IHotelAccessService hotelAccess) : base(service)
    {
        _guestService = service;
        _hotelAccess = hotelAccess;
    }

    /// <summary>
    /// Guests of the hotels the current user works at
    /// </summary>
    [HttpGet]
    [Authorize(Roles = ManagementRoles)]
    public override async Task<IActionResult> GetAllAsync()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _guestService.GetGuestsForHotelsAsync(hotelIds));
    }

    [HttpGet("all-unfiltered")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> GetAllUnfilteredAsync()
    {
        return Ok(await _guestService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = ManagementRoles)]
    public override async Task<IActionResult> GetByIdAsync(int id)
    {
        if (!await _hotelAccess.CanAccessGuestAsync(id))
            return NotFound();

        var guest = await _guestService.GetByIdAsync(id);
        return guest == null ? NotFound() : Ok(guest);
    }

    /// <summary>
    /// Create a walk-in guest for one of the user's hotels
    /// </summary>
    [HttpPost]
    [Authorize(Roles = ManagementRoles)]
    public override async Task<IActionResult> CreateAsync([FromBody] GuestDto dto)
    {
        if (!dto.HotelId.HasValue)
            return BadRequest(new { message = "A hotel must be selected for the guest" });

        if (!await _hotelAccess.CanAccessHotelAsync(dto.HotelId.Value))
            return Forbid();

        var created = await _guestService.CreateAsync(dto);
        return CreatedAtAction("GetById", new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = ManagementRoles)]
    public override async Task<IActionResult> UpdateAsync(int id, [FromBody] GuestDto dto)
    {
        if (!await _hotelAccess.CanAccessGuestAsync(id))
            return NotFound();

        return Ok(await _guestService.UpdateAsync(id, dto));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = AdminRoles)]
    public override async Task<IActionResult> DeleteAsync(int id)
    {
        if (!await _hotelAccess.CanAccessGuestAsync(id))
            return NotFound();

        await _guestService.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("search")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> SearchByNameAsync([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Search term cannot be empty" });

        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _guestService.SearchByNameAsync(name, hotelIds));
    }

    [HttpGet("email/{email}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetByEmailAsync(string email)
    {
        var guest = await _guestService.GetByEmailAsync(email);
        if (guest == null || !await _hotelAccess.CanAccessGuestAsync(guest.Id))
            return NotFound();

        return Ok(guest);
    }

    [HttpGet("phone/{phoneNumber}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetByPhoneNumberAsync(string phoneNumber)
    {
        var guest = await _guestService.GetByPhoneNumberAsync(phoneNumber);
        if (guest == null || !await _hotelAccess.CanAccessGuestAsync(guest.Id))
            return NotFound();

        return Ok(guest);
    }

    [HttpGet("vip")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetVIPGuestsAsync()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _guestService.GetVIPGuestsAsync(hotelIds));
    }

    [HttpGet("active")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetActiveGuestsAsync()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _guestService.GetActiveGuestsAsync(hotelIds));
    }

    [HttpGet("blacklisted")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> GetBlacklistedGuestsAsync()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _guestService.GetBlacklistedGuestsAsync(hotelIds));
    }

    [HttpPost("{id:int}/blacklist")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> BlacklistGuestAsync(int id, [FromBody] BlacklistRequest request)
    {
        if (!await _hotelAccess.CanAccessGuestAsync(id))
            return NotFound();

        await _guestService.BlacklistGuestAsync(id, request.Reason);
        return Ok(new { message = "Guest blacklisted successfully" });
    }

    [HttpPost("{id:int}/unblacklist")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> UnblacklistGuestAsync(int id)
    {
        if (!await _hotelAccess.CanAccessGuestAsync(id))
            return NotFound();

        await _guestService.UnblacklistGuestAsync(id);
        return Ok(new { message = "Guest removed from blacklist" });
    }

    [HttpPatch("{id:int}/vip")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> SetVIPStatusAsync(int id, [FromBody] VIPStatusRequest request)
    {
        if (!await _hotelAccess.CanAccessGuestAsync(id))
            return NotFound();

        await _guestService.SetVIPStatusAsync(id, request.IsVIP);
        return Ok(new { message = $"Guest VIP status updated to {request.IsVIP}" });
    }

    [HttpGet("hotel/{hotelId:int}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetGuestsByHotelAsync(int hotelId)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _guestService.GetGuestsByHotelIdAsync(hotelId));
    }

    [HttpGet("my-guests")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetMyGuestsAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _guestService.GetGuestsCreatedByUserAsync(userId));
    }

    /// <summary>
    /// The current user's own guest profile, created from their account on first use
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetOrCreateMyGuestProfileAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _guestService.GetOrCreateGuestProfileAsync(userId));
    }
}

public class BlacklistRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class VIPStatusRequest
{
    public bool IsVIP { get; set; }
}
