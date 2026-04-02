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

    public WalkInController(
        IReservationService reservationService,
        IGuestService guestService,
        ApplicationDbContext context)
    {
        _reservationService = reservationService;
        _guestService = guestService;
        _context = context;
    }

    [HttpGet("available-rooms/{hotelId}")]
    public async Task<IActionResult> GetAvailableRoomsTonight(int hotelId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var rooms = await _reservationService.GetAvailableRoomsAsync(hotelId, today, tomorrow, BookingType.Daily);
        return Ok(rooms);
    }

    [HttpGet("guest-intelligence/{guestId}")]
    public async Task<IActionResult> GetGuestIntelligence(int guestId)
    {
        var guest = await _context.Guests
            .Include(g => g.Reservations).ThenInclude(r => r.Room)
            .FirstOrDefaultAsync(g => g.Id == guestId);

        if (guest == null) return NotFound();

        var completedReservations = guest.Reservations
            .Where(r => r.Status == ReservationStatus.CheckedOut)
            .OrderByDescending(r => r.CheckOutDate)
            .ToList();

        var mostUsedRoomType = completedReservations
            .GroupBy(r => r.Room?.Type.ToString())
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var hasOutstanding = guest.Reservations
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
            LastStayDate = guest.LastStayDate,
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Resolve or create guest
        int guestId;
        if (dto.ExistingGuestId.HasValue)
        {
            guestId = dto.ExistingGuestId.Value;
        }
        else if (dto.NewGuest != null)
        {
            var existingByEmail = await _guestService.GetByEmailAsync(dto.NewGuest.Email);
            if (existingByEmail != null)
            {
                guestId = existingByEmail.Id;
            }
            else
            {
                var newGuest = await _guestService.CreateAsync(new GuestDto
                {
                    FirstName = dto.NewGuest.FirstName,
                    LastName = dto.NewGuest.LastName,
                    Email = dto.NewGuest.Email,
                    PhoneNumber = dto.NewGuest.PhoneNumber,
                    IdentificationNumber = dto.NewGuest.IdentificationNumber,
                    IdentificationType = dto.NewGuest.IdentificationType,
                    Nationality = dto.NewGuest.Nationality,
                    HotelId = dto.HotelId,
                    CreatedByUserId = userId
                });
                guestId = newGuest.Id;
            }
        }
        else
        {
            return BadRequest(new { message = "Either ExistingGuestId or NewGuest must be provided" });
        }

        // Create reservation
        var createDto = new CreateReservationDto
        {
            HotelId = dto.HotelId,
            RoomId = dto.RoomId,
            GuestId = guestId,
            BookingType = dto.BookingType,
            CheckInDate = dto.CheckInDate,
            CheckOutDate = dto.CheckOutDate,
            DurationInHours = dto.DurationInHours,
            NumberOfGuests = dto.NumberOfGuests,
            DepositAmount = dto.DepositAmount,
            PaymentMethod = dto.PaymentMethod,
            SpecialRequests = dto.SpecialRequests
        };

        var reservation = await _reservationService.CreateReservationAsync(createDto);

        // Apply discount if provided
        if (dto.DiscountAmount > 0 || dto.OverridePrice.HasValue)
        {
            var entity = await _context.Reservations.FindAsync(reservation.Id);
            if (entity != null)
            {
                if (dto.DiscountAmount > 0)
                {
                    entity.DiscountAmount = dto.DiscountAmount;
                    entity.DiscountReason = dto.DiscountReason;
                    entity.TotalAmount = Math.Max(0, entity.TotalAmount - dto.DiscountAmount);
                    entity.RemainingAmount = Math.Max(0, entity.TotalAmount - entity.DepositAmount);
                }
                else if (dto.OverridePrice.HasValue)
                {
                    entity.TotalAmount = dto.OverridePrice.Value;
                    entity.RemainingAmount = Math.Max(0, dto.OverridePrice.Value - entity.DepositAmount);
                }
                await _context.SaveChangesAsync();
            }
        }

        // Confirm then check-in immediately
        reservation = await _reservationService.ConfirmReservationAsync(reservation.Id);
        reservation = await _reservationService.CheckInReservationAsync(reservation.Id);

        return Ok(reservation);
    }

    [HttpPost("express-checkout/{reservationId}")]
    public async Task<IActionResult> ExpressCheckOut(int reservationId, [FromBody] ExpressCheckOutDto dto)
    {
        var entity = await _context.Reservations.FindAsync(reservationId)
            ?? throw new KeyNotFoundException($"Reservation {reservationId} not found");

        if (entity.Status != ReservationStatus.CheckedIn)
            return BadRequest(new { message = "Reservation must be in CheckedIn status to check out" });

        // Apply extra charges
        if (dto.ExtraCharges > 0)
        {
            entity.ExtraCharges = dto.ExtraCharges;
            entity.ExtraChargesNotes = dto.ExtraChargesNotes;
            entity.TotalAmount += dto.ExtraCharges;
            entity.RemainingAmount = Math.Max(0, entity.TotalAmount - entity.DepositAmount);
            await _context.SaveChangesAsync();
        }

        // Record final payment if provided
        if (dto.FinalPayment.HasValue && dto.FinalPayment > 0)
        {
            await _reservationService.RecordPaymentAsync(
                reservationId,
                dto.FinalPayment.Value,
                dto.PaymentMethod ?? PaymentMethod.Cash);
        }

        // Check out
        var result = await _reservationService.CheckOutReservationAsync(reservationId);
        return Ok(result);
    }

    [HttpPatch("guest-flags/{guestId}")]
    public async Task<IActionResult> UpdateGuestFlags(int guestId, [FromBody] UpdateGuestFlagsDto dto)
    {
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
