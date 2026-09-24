using System.Security.Claims;
using HotelManagement.Authorization.Requirements;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers;

/// <summary>
/// Controller for managing reservations/bookings.
/// Staff only ever see reservations at hotels they can access (see IHotelAccessService);
/// guests only see reservations made for their own guest profile.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private const string ManagementRoles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}";

    private readonly IReservationService _reservationService;
    private readonly IRoomService _roomService;
    private readonly IGuestService _guestService;
    private readonly IHotelAccessService _hotelAccess;
    private readonly IAuthorizationService _authorizationService;

    public ReservationsController(
        IReservationService reservationService,
        IRoomService roomService,
        IGuestService guestService,
        IHotelAccessService hotelAccess,
        IAuthorizationService authorizationService)
    {
        _reservationService = reservationService;
        _roomService = roomService;
        _guestService = guestService;
        _hotelAccess = hotelAccess;
        _authorizationService = authorizationService;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<bool> CanAccessReservationAsync(int id)
    {
        var result = await _authorizationService.AuthorizeAsync(User, id, new ReservationAccessRequirement());
        return result.Succeeded;
    }

    private async Task<bool> CanAccessRoomAsync(int roomId)
    {
        var hotelId = await _hotelAccess.GetRoomHotelIdAsync(roomId);
        return hotelId.HasValue && await _hotelAccess.CanAccessHotelAsync(hotelId.Value);
    }

    /// <summary>
    /// Create a new reservation. A guest's booking is always for their own guest profile
    /// (whatever GuestId was sent); staff can only book at their hotels.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{ManagementRoles},{AppRoles.Guest}")]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationDto createDto)
    {
        if (User.IsInRole(AppRoles.Guest))
        {
            var myProfile = await _guestService.GetOrCreateGuestProfileAsync(CurrentUserId!);
            createDto.GuestId = myProfile.Id;

            // Only staff can record money received; a guest's booking starts unpaid
            createDto.DepositAmount = 0;
            createDto.PaymentReference = null;
            createDto.Notes = null;
        }
        else if (!await _hotelAccess.CanAccessHotelAsync(createDto.HotelId))
        {
            return Forbid();
        }

        var reservation = await _reservationService.CreateReservationAsync(createDto);
        return CreatedAtAction(nameof(GetReservationById), new { id = reservation.Id }, reservation);
    }

    /// <summary>
    /// Get reservation by ID (with authorization check)
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetReservationById(int id)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id);
        if (reservation == null)
            return NotFound();

        if (!await CanAccessReservationAsync(id))
            return Forbid();

        return Ok(reservation);
    }

    /// <summary>
    /// Get all reservations visible to the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllReservations()
    {
        if (User.IsInRole(AppRoles.Guest))
            return Ok(await _reservationService.GetGuestUserReservationsAsync(CurrentUserId!));

        if (User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager))
        {
            var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
            return Ok(await _reservationService.GetReservationsForHotelsAsync(hotelIds));
        }

        return Ok(Array.Empty<ReservationDto>());
    }

    /// <summary>
    /// Update a reservation (with authorization check)
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateReservation(int id, [FromBody] UpdateReservationDto updateDto)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        // Guests may change their stay details, but payment details and internal staff notes are staff-only
        if (User.IsInRole(AppRoles.Guest))
        {
            var existing = await _reservationService.GetReservationByIdAsync(id);
            if (existing == null)
                return NotFound();

            updateDto.PaymentMethod = existing.PaymentMethod;
            updateDto.PaymentReference = existing.PaymentReference;
            updateDto.Notes = existing.Notes;
        }

        var reservation = await _reservationService.UpdateReservationAsync(id, updateDto);
        return Ok(reservation);
    }

    /// <summary>
    /// Delete a reservation (SuperAdmin and Admin only with authorization check)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteReservation(int id)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        await _reservationService.DeleteReservationAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Get reservations by hotel (with hotel access check)
    /// </summary>
    [HttpGet("hotel/{hotelId:int}")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> GetReservationsByHotel(int hotelId)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _reservationService.GetReservationsByHotelAsync(hotelId));
    }

    /// <summary>
    /// Get reservations by room
    /// </summary>
    [HttpGet("room/{roomId:int}")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> GetReservationsByRoom(int roomId)
    {
        if (!await CanAccessRoomAsync(roomId))
            return Forbid();

        return Ok(await _reservationService.GetReservationsByRoomAsync(roomId));
    }

    /// <summary>
    /// Get reservations by guest. Guests may only ask for their own profile;
    /// staff see the guest's reservations at their own hotels.
    /// </summary>
    [HttpGet("guest/{guestId:int}")]
    public async Task<IActionResult> GetReservationsByGuest(int guestId)
    {
        if (User.IsInRole(AppRoles.Guest))
        {
            var myProfile = await _guestService.GetByUserIdAsync(CurrentUserId!);
            if (myProfile == null || myProfile.Id != guestId)
                return Forbid();

            return Ok(await _reservationService.GetGuestUserReservationsAsync(CurrentUserId!));
        }

        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _reservationService.GetReservationsByGuestAsync(guestId, hotelIds));
    }

    /// <summary>
    /// Get reservations by status
    /// </summary>
    [HttpGet("status/{status}")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> GetReservationsByStatus(ReservationStatus status)
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _reservationService.GetReservationsByStatusAsync(status, hotelIds));
    }

    /// <summary>
    /// Get reservations overlapping a date range
    /// </summary>
    [HttpGet("daterange")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> GetReservationsByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _reservationService.GetReservationsByDateRangeAsync(startDate, endDate, hotelIds));
    }

    /// <summary>
    /// Get my reservations (reservations for the current user's guest profile)
    /// </summary>
    [HttpGet("my-reservations")]
    public async Task<IActionResult> GetMyReservations()
    {
        return Ok(await _reservationService.GetGuestUserReservationsAsync(CurrentUserId!));
    }

    /// <summary>
    /// Check room availability
    /// </summary>
    [HttpGet("room/{roomId:int}/availability")]
    public async Task<IActionResult> CheckRoomAvailability(
        int roomId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut)
    {
        var isAvailable = await _reservationService.IsRoomAvailableAsync(roomId, checkIn, checkOut);
        return Ok(new { roomId, checkIn, checkOut, isAvailable });
    }

    /// <summary>
    /// Get available rooms for a hotel with optional filters (public, used for booking)
    /// </summary>
    [HttpGet("available-rooms")]
    public async Task<IActionResult> GetAvailableRooms(
        [FromQuery] int hotelId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut,
        [FromQuery] BookingType bookingType = BookingType.Daily,
        [FromQuery] int? minCapacity = null,
        [FromQuery] string? roomType = null)
    {
        var availableRooms = (await _reservationService.GetAvailableRoomsAsync(
            hotelId, checkIn, checkOut, bookingType, minCapacity, roomType)).ToList();

        return Ok(new
        {
            hotelId,
            checkIn,
            checkOut,
            bookingType,
            totalAvailable = availableRooms.Count,
            rooms = availableRooms
        });
    }

    /// <summary>
    /// Get conflicting reservations for a room
    /// </summary>
    [HttpGet("room/{roomId:int}/conflicts")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetConflictingReservations(
        int roomId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut)
    {
        if (!await CanAccessRoomAsync(roomId))
            return Forbid();

        return Ok(await _reservationService.GetConflictingReservationsAsync(roomId, checkIn, checkOut));
    }

    [HttpPost("{id:int}/confirm")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> ConfirmReservation(int id)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        return Ok(await _reservationService.ConfirmReservationAsync(id));
    }

    [HttpPost("{id:int}/checkin")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> CheckInReservation(int id)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        return Ok(await _reservationService.CheckInReservationAsync(id));
    }

    [HttpPost("{id:int}/checkout")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> CheckOutReservation(int id)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        return Ok(await _reservationService.CheckOutReservationAsync(id));
    }

    /// <summary>
    /// Cancel a reservation (guests: their own; staff: their hotels)
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelReservation(int id, [FromBody] CancelReservationRequest request)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        return Ok(await _reservationService.CancelReservationAsync(id, request.Reason));
    }

    [HttpPost("{id:int}/noshow")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> MarkAsNoShow(int id)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        return Ok(await _reservationService.MarkAsNoShowAsync(id));
    }

    [HttpPost("{id:int}/payment")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> RecordPayment(int id, [FromBody] RecordPaymentRequest request)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        return Ok(await _reservationService.RecordPaymentAsync(id, request.Amount, request.PaymentMethod, request.Reference));
    }

    [HttpPost("{id:int}/refund")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> RecordRefund(int id, [FromBody] RecordRefundRequest request)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        return Ok(await _reservationService.RecordRefundAsync(id, request.Amount, request.Reason));
    }

    /// <summary>
    /// Payment ledger of a reservation
    /// </summary>
    [HttpGet("{id:int}/payments")]
    public async Task<IActionResult> GetPayments(int id)
    {
        if (!await CanAccessReservationAsync(id))
            return Forbid();

        return Ok(await _reservationService.GetPaymentsAsync(id));
    }

    [HttpGet("stats/count")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetTotalCount()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(new { totalReservations = await _reservationService.GetTotalReservationsCountAsync(hotelIds) });
    }

    [HttpGet("stats/revenue")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetTotalRevenue()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(new { totalRevenue = await _reservationService.GetTotalRevenueAsync(hotelIds) });
    }

    [HttpGet("stats/by-status")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetCountByStatus()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _reservationService.GetReservationCountByStatusAsync(hotelIds));
    }

    [HttpGet("stats/by-month/{year:int}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetCountByMonth(int year)
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _reservationService.GetReservationCountByMonthAsync(year, hotelIds));
    }

    [HttpGet("today/check-ins")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetTodaysCheckIns()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _reservationService.GetCheckInsOnAsync(DateTime.UtcNow.Date, hotelIds));
    }

    [HttpGet("today/check-outs")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetTodaysCheckOuts()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        return Ok(await _reservationService.GetCheckOutsOnAsync(DateTime.UtcNow.Date, hotelIds));
    }

    /// <summary>
    /// Revenue breakdown analytics for the user's hotels
    /// </summary>
    [HttpGet("analytics/revenue-breakdown")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> GetRevenueBreakdown([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var start = startDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var end = endDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

        if ((end - start).Days > 365)
            return BadRequest(new { message = "Date range cannot exceed 365 days" });

        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();
        var userRooms = (await _roomService.GetRoomsForHotelsAsync(hotelIds)).ToList();
        var roomsDict = userRooms.ToDictionary(r => r.Id);

        // Reservations that started in the period (the overlap query also returns earlier ones, filtered out here)
        var periodReservations = (await _reservationService.GetReservationsByDateRangeAsync(start, end, hotelIds))
            .Where(r => r.CheckInDate >= start && r.CheckInDate <= end)
            .ToList();
        var completedReservations = periodReservations
            .Where(r => r.Status == ReservationStatus.CheckedOut)
            .ToList();

        var totalRevenue = completedReservations.Sum(r => r.TotalAmount);
        var depositRevenue = completedReservations.Sum(r => r.DepositAmount);

        var revenueByRoomType = completedReservations
            .Where(r => roomsDict.ContainsKey(r.RoomId))
            .GroupBy(r => roomsDict[r.RoomId].Type)
            .Select(g => new
            {
                RoomType = g.Key,
                RoomTypeName = g.Key.ToString(),
                Revenue = g.Sum(r => r.TotalAmount),
                Count = g.Count(),
                Percentage = totalRevenue > 0 ? Math.Round(g.Sum(r => r.TotalAmount) / totalRevenue * 100, 1) : 0
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var revenueByPaymentMethod = completedReservations
            .GroupBy(r => r.PaymentMethod)
            .Select(g => new
            {
                PaymentMethod = g.Key,
                PaymentMethodName = g.Key.ToString(),
                Revenue = g.Sum(r => r.TotalAmount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var revenueByBookingType = completedReservations
            .GroupBy(r => r.BookingType)
            .Select(g => new
            {
                BookingType = g.Key,
                BookingTypeName = g.Key.ToString(),
                Revenue = g.Sum(r => r.TotalAmount),
                Count = g.Count(),
                AveragePrice = g.Average(r => r.TotalAmount)
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var cancellationCount = periodReservations.Count(r => r.Status == ReservationStatus.Cancelled);

        // Short stays count as a fraction of a room-night
        var totalRoomNights = completedReservations.Sum(r => r.BookingType == BookingType.ShortStay
            ? (r.DurationInHours ?? 0) / 24m
            : (r.CheckOutDate.Date - r.CheckInDate.Date).Days);

        var adr = totalRoomNights > 0 ? totalRevenue / totalRoomNights : 0;
        var totalAvailableRoomNights = (decimal)(userRooms.Count * (end.Date - start.Date).Days);
        var revPar = totalAvailableRoomNights > 0 ? totalRevenue / totalAvailableRoomNights : 0;

        return Ok(new
        {
            TotalRevenue = Math.Round(totalRevenue, 2),
            RoomRevenue = Math.Round(totalRevenue, 2),
            DepositRevenue = Math.Round(depositRevenue, 2),
            CompletedReservations = completedReservations.Count,
            CancellationCount = cancellationCount,
            RevenueByRoomType = revenueByRoomType,
            RevenueByPaymentMethod = revenueByPaymentMethod,
            RevenueByBookingType = revenueByBookingType,
            AverageDailyRate = Math.Round(adr, 2),
            RevPAR = Math.Round(revPar, 2),
            TotalRoomNights = Math.Round(totalRoomNights, 1),
            PeriodStart = start,
            PeriodEnd = end
        });
    }
}

/// <summary>
/// Request DTO for cancelling a reservation
/// </summary>
public class CancelReservationRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for recording a payment
/// </summary>
public class RecordPaymentRequest
{
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Reference { get; set; }
}

/// <summary>
/// Request DTO for recording a refund
/// </summary>
public class RecordRefundRequest
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}
