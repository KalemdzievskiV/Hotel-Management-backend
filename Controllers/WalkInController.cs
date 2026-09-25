using System.Security.Claims;
using HotelManagement.Data;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
public class WalkInController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly IGuestService _guestService;
    private readonly ApplicationDbContext _context;
    private readonly IHotelAccessService _hotelAccess;
    private readonly IWalkInService _walkInService;

    public WalkInController(
        IReservationService reservationService,
        IGuestService guestService,
        ApplicationDbContext context,
        IHotelAccessService hotelAccess,
        IWalkInService walkInService)
    {
        _hotelAccess = hotelAccess;
        _walkInService = walkInService;
        _reservationService = reservationService;
        _guestService = guestService;
        _context = context;
    }

    [HttpGet("available-rooms/{hotelId}")]
    public async Task<IActionResult> GetAvailableRoomsTonight(int hotelId)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var rooms = await _reservationService.GetAvailableRoomsAsync(hotelId, today, tomorrow, BookingType.Daily);
        return Ok(rooms);
    }

    [HttpGet("guest-intelligence/{guestId}")]
    public async Task<IActionResult> GetGuestIntelligence(int guestId)
    {
        if (!await _hotelAccess.CanAccessGuestAsync(guestId))
            return NotFound();

        var guest = await _context.Guests
            .Include(g => g.Reservations).ThenInclude(r => r.Room)
            .FirstOrDefaultAsync(g => g.Id == guestId);

        if (guest == null) return NotFound();

        // Stays at other hotels are those hotels' business, same as the guest's reservation list
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        var reservationsHere = guest.Reservations.Where(r => hotelIds.Contains(r.HotelId)).ToList();

        var completedReservations = reservationsHere
            .Where(r => r.Status == ReservationStatus.CheckedOut)
            .OrderByDescending(r => r.CheckOutDate)
            .ToList();

        var mostUsedRoomType = completedReservations
            .GroupBy(r => r.Room?.Type.ToString())
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var hasOutstanding = reservationsHere
            .Any(r => r.IsActive && r.PaymentStatus != PaymentStatus.Paid);

        var intelligence = new GuestIntelligenceDto
        {
            GuestId = guest.Id,
            FullName = $"{guest.FirstName} {guest.LastName}",
            Email = guest.Email,
            PhoneNumber = guest.PhoneNumber,
            IsVIP = guest.IsVIP,
            IsBlacklisted = guest.IsBlacklisted,
            BlacklistReason = guest.BlacklistReason,
            Preferences = guest.Preferences,
            SpecialRequests = guest.SpecialRequests,
            Notes = guest.Notes,
            TotalStays = completedReservations.Count,
            TotalSpent = completedReservations.Sum(r => r.TotalAmount),
            LastStayDate = completedReservations.Select(r => (DateTime?)(r.CheckedOutAt ?? r.CheckOutDate)).FirstOrDefault(),
            MostUsedRoomType = mostUsedRoomType,
            HasOutstandingPayments = hasOutstanding,
            RecentStays = completedReservations
                .Take(5)
                .Select(r => new GuestStaySummaryDto
                {
                    ReservationId = r.Id,
                    RoomNumber = r.Room?.RoomNumber ?? string.Empty,
                    RoomType = r.Room?.Type.ToString() ?? string.Empty,
                    CheckInDate = r.CheckInDate,
                    CheckOutDate = r.CheckOutDate,
                    TotalAmount = r.TotalAmount,
                    PaymentStatus = r.PaymentStatus.ToString(),
                    Status = r.Status.ToString()
                })
                .ToList()
        };

        return Ok(intelligence);
    }

    [HttpPost("quick-checkin")]
    public async Task<IActionResult> QuickCheckIn([FromBody] QuickCheckInDto dto)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(dto.HotelId))
            return Forbid();

        if (dto.ExistingGuestId.HasValue && !await _hotelAccess.CanAccessGuestAsync(dto.ExistingGuestId.Value))
            return NotFound(new { message = "Guest not found" });

        if (dto.ExistingGuestId == null && dto.NewGuest == null)
            return BadRequest(new { message = "Either ExistingGuestId or NewGuest must be provided" });

        return Ok(await _walkInService.QuickCheckInAsync(dto));
    }

    [HttpPost("express-checkout/{reservationId}")]
    public async Task<IActionResult> ExpressCheckOut(int reservationId, [FromBody] ExpressCheckOutDto dto)
    {
        var entity = await _context.Reservations.FindAsync(reservationId)
            ?? throw new KeyNotFoundException($"Reservation {reservationId} not found");

        if (!await _hotelAccess.CanAccessHotelAsync(entity.HotelId))
            return Forbid();

        if (entity.Status != ReservationStatus.CheckedIn)
            return BadRequest(new { message = "Reservation must be in CheckedIn status to check out" });

        return Ok(await _walkInService.ExpressCheckOutAsync(reservationId, dto));
    }

    [HttpPatch("guest-flags/{guestId}")]
    public async Task<IActionResult> UpdateGuestFlags(int guestId, [FromBody] UpdateGuestFlagsDto dto)
    {
        if (!await _hotelAccess.CanAccessGuestAsync(guestId))
            return NotFound();

        // Blacklisting is an Admin decision, same as the dedicated Guests endpoints
        if (dto.IsBlacklisted.HasValue && !User.IsInRole(AppRoles.SuperAdmin) && !User.IsInRole(AppRoles.Admin))
            return Forbid();

        var guest = await _context.Guests.FindAsync(guestId)
            ?? throw new KeyNotFoundException($"Guest {guestId} not found");

        if (dto.IsVIP.HasValue) guest.IsVIP = dto.IsVIP.Value;

        if (dto.IsBlacklisted.HasValue)
        {
            guest.IsBlacklisted = dto.IsBlacklisted.Value;
            if (dto.IsBlacklisted.Value && dto.BlacklistReason != null)
                guest.BlacklistReason = dto.BlacklistReason;
            else if (!dto.IsBlacklisted.Value)
                guest.BlacklistReason = null;
        }

        if (dto.Notes != null) guest.Notes = dto.Notes;
        guest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Guest flags updated" });
    }
}
