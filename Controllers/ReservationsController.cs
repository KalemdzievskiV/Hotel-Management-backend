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
/// Controller for managing reservations/bookings
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly IHotelService _hotelService;
    private readonly IRoomService _roomService;
    private readonly IAuthorizationService _authorizationService;

    public ReservationsController(
        IReservationService reservationService, 
        IHotelService hotelService,
        IRoomService roomService,
        IAuthorizationService authorizationService)
    {
        _reservationService = reservationService;
        _roomService = roomService;
        _hotelService = hotelService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Create a new reservation
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationDto createDto)
    {
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

        // Check authorization - use reservation ID for authorization handler
        var authResult = await _authorizationService.AuthorizeAsync(
            User, id, new ReservationAccessRequirement());
        
        if (!authResult.Succeeded)
            return Forbid();

        return Ok(reservation);
    }

    /// <summary>
    /// Get all reservations (filtered by user's role and access)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllReservations()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var isManager = User.IsInRole(AppRoles.Manager);
        var isGuest = User.IsInRole(AppRoles.Guest);
        
        if (isSuperAdmin)
        {
            // SuperAdmin sees all reservations
            var reservations = await _reservationService.GetAllReservationsAsync();
            return Ok(reservations);
        }
        
        if (isAdmin || isManager)
        {
            // Admin/Manager sees only reservations from their hotels (service filters hotels)
            var userHotels = await _hotelService.GetAllAsync();
            var hotelIds = userHotels.Select(h => h.Id).ToList();
            
            var allReservations = await _reservationService.GetAllReservationsAsync();
            var filteredReservations = allReservations.Where(r => hotelIds.Contains(r.HotelId));
            
            return Ok(filteredReservations);
        }
        
        if (isGuest)
        {
            // Guest sees only their own reservations
            var allReservations = await _reservationService.GetAllReservationsAsync();
            var guestReservations = allReservations.Where(r => r.CreatedByUserId == userId);
            return Ok(guestReservations);
        }
        
        return Ok(new List<Reservation>());
    }

    /// <summary>
    /// Update a reservation (with authorization check)
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateReservation(int id, [FromBody] UpdateReservationDto updateDto)
    {
        // Check authorization first
        var authResult = await _authorizationService.AuthorizeAsync(
            User, id, new ReservationAccessRequirement());
        
        if (!authResult.Succeeded)
            return Forbid();

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
        // Check authorization
        var authResult = await _authorizationService.AuthorizeAsync(
            User, id, new ReservationAccessRequirement());
        
        if (!authResult.Succeeded)
            return Forbid();

        await _reservationService.DeleteReservationAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Get reservations by hotel (with hotel ownership check)
    /// </summary>
    [HttpGet("hotel/{hotelId:int}")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> GetReservationsByHotel(int hotelId)
    {
        // Check hotel authorization
        var authResult = await _authorizationService.AuthorizeAsync(
            User, hotelId, new HotelOwnershipRequirement());
        
        if (!authResult.Succeeded)
            return Forbid();

        var reservations = await _reservationService.GetReservationsByHotelAsync(hotelId);
        return Ok(reservations);
    }

    /// <summary>
    /// Get reservations by room
    /// </summary>
    [HttpGet("room/{roomId:int}")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> GetReservationsByRoom(int roomId)
    {
        var reservations = await _reservationService.GetReservationsByRoomAsync(roomId);
        return Ok(reservations);
    }

    /// <summary>
    /// Get reservations by guest
    /// </summary>
    [HttpGet("guest/{guestId:int}")]
    public async Task<IActionResult> GetReservationsByGuest(int guestId)
    {
        var reservations = await _reservationService.GetReservationsByGuestAsync(guestId);
        return Ok(reservations);
    }

    /// <summary>
    /// Get reservations by status
    /// </summary>
    [HttpGet("status/{status}")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> GetReservationsByStatus(ReservationStatus status)
    {
        var reservations = await _reservationService.GetReservationsByStatusAsync(status);
        return Ok(reservations);
    }

    /// <summary>
    /// Get reservations by date range
    /// </summary>
    [HttpGet("daterange")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> GetReservationsByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var reservations = await _reservationService.GetReservationsByDateRangeAsync(startDate, endDate);
        return Ok(reservations);
    }

    /// <summary>
    /// Get my reservations (current user)
    /// </summary>
    [HttpGet("my-reservations")]
    public async Task<IActionResult> GetMyReservations()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var reservations = await _reservationService.GetUserReservationsAsync(userId);
        return Ok(reservations);
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
    /// Get available rooms for a hotel with optional filters
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
        var availableRooms = await _reservationService.GetAvailableRoomsAsync(
            hotelId, 
            checkIn, 
            checkOut, 
            bookingType,
            minCapacity, 
            roomType);
        
        return Ok(new 
        { 
            hotelId, 
            checkIn, 
            checkOut, 
            bookingType,
            totalAvailable = availableRooms.Count(),
            rooms = availableRooms 
        });
    }

    /// <summary>
    /// Get conflicting reservations for a room
    /// </summary>
    [HttpGet("room/{roomId:int}/conflicts")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetConflictingReservations(
        int roomId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut)
    {
        var conflicts = await _reservationService.GetConflictingReservationsAsync(roomId, checkIn, checkOut);
        return Ok(conflicts);
    }

    /// <summary>
    /// Confirm a reservation
    /// </summary>
    [HttpPost("{id:int}/confirm")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> ConfirmReservation(int id)
    {
        var reservation = await _reservationService.ConfirmReservationAsync(id);
        return Ok(reservation);
    }

    /// <summary>
    /// Check in a reservation
    /// </summary>
    [HttpPost("{id:int}/checkin")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> CheckInReservation(int id)
    {
        var reservation = await _reservationService.CheckInReservationAsync(id);
        return Ok(reservation);
    }

    /// <summary>
    /// Check out a reservation
    /// </summary>
    [HttpPost("{id:int}/checkout")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> CheckOutReservation(int id)
    {
        var reservation = await _reservationService.CheckOutReservationAsync(id);
        return Ok(reservation);
    }

    /// <summary>
    /// Cancel a reservation
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelReservation(int id, [FromBody] CancelReservationRequest request)
    {
        var reservation = await _reservationService.CancelReservationAsync(id, request.Reason);
        return Ok(reservation);
    }

    /// <summary>
    /// Mark reservation as no-show
    /// </summary>
    [HttpPost("{id:int}/noshow")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> MarkAsNoShow(int id)
    {
        var reservation = await _reservationService.MarkAsNoShowAsync(id);
        return Ok(reservation);
    }

    /// <summary>
    /// Record a payment for reservation
    /// </summary>
    [HttpPost("{id:int}/payment")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> RecordPayment(int id, [FromBody] RecordPaymentRequest request)
    {
        var reservation = await _reservationService.RecordPaymentAsync(
            id, 
            request.Amount, 
            request.PaymentMethod, 
            request.Reference);
        return Ok(reservation);
    }

    /// <summary>
    /// Record a refund for reservation
    /// </summary>
    [HttpPost("{id:int}/refund")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> RecordRefund(int id, [FromBody] RecordRefundRequest request)
    {
        var reservation = await _reservationService.RecordRefundAsync(id, request.Amount, request.Reason);
        return Ok(reservation);
    }

    /// <summary>
    /// Get total reservations count
    /// </summary>
    [HttpGet("stats/count")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetTotalCount()
    {
        var count = await _reservationService.GetTotalReservationsCountAsync();
        return Ok(new { totalReservations = count });
    }

    /// <summary>
    /// Get total revenue
    /// </summary>
    [HttpGet("stats/revenue")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetTotalRevenue()
    {
        var revenue = await _reservationService.GetTotalRevenueAsync();
        return Ok(new { totalRevenue = revenue });
    }

    /// <summary>
    /// Get reservation count by status
    /// </summary>
    [HttpGet("stats/by-status")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetCountByStatus()
    {
        var stats = await _reservationService.GetReservationCountByStatusAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Get reservation count by month for a year
    /// </summary>
    [HttpGet("stats/by-month/{year:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetCountByMonth(int year)
    {
        var stats = await _reservationService.GetReservationCountByMonthAsync(year);
        return Ok(stats);
    }

    /// <summary>
    /// Get today's check-ins (filtered by user's hotels)
    /// </summary>
    [HttpGet("today/check-ins")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetTodaysCheckIns()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isGuest = User.IsInRole(AppRoles.Guest);
        
        if (isGuest)
        {
            return Forbid(); // Guests don't need this view
        }

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        
        // Get user's hotels
        var userHotels = await _hotelService.GetAllAsync();
        var hotelIds = userHotels.Select(h => h.Id).ToList();
        
        // Get all reservations for today's check-in
        var allReservations = await _reservationService.GetAllReservationsAsync();
        
        var todaysCheckIns = allReservations
            .Where(r => hotelIds.Contains(r.HotelId) &&
                       r.CheckInDate.Date == today &&
                       (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.CheckedIn))
            .OrderBy(r => r.CheckInDate)
            .ToList();
        
        return Ok(todaysCheckIns);
    }

    /// <summary>
    /// Get today's check-outs (filtered by user's hotels)
    /// </summary>
    [HttpGet("today/check-outs")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetTodaysCheckOuts()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isGuest = User.IsInRole(AppRoles.Guest);
        
        if (isGuest)
        {
            return Forbid(); // Guests don't need this view
        }

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        
        // Get user's hotels
        var userHotels = await _hotelService.GetAllAsync();
        var hotelIds = userHotels.Select(h => h.Id).ToList();
        
        // Get all reservations for today's check-out
        var allReservations = await _reservationService.GetAllReservationsAsync();
        
        var todaysCheckOuts = allReservations
            .Where(r => hotelIds.Contains(r.HotelId) &&
                       r.CheckOutDate.Date == today &&
                       (r.Status == ReservationStatus.CheckedIn || r.Status == ReservationStatus.CheckedOut))
            .OrderBy(r => r.CheckOutDate)
            .ToList();
        
        return Ok(todaysCheckOuts);
    }

    /// <summary>
    /// Get revenue breakdown analytics (filtered by user's hotels)
    /// </summary>
    [HttpGet("analytics/revenue-breakdown")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetRevenueBreakdown([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        
        // Get user's hotels
        List<int> hotelIds;
        if (isSuperAdmin)
        {
            var allHotels = await _hotelService.GetAllAsync();
            hotelIds = allHotels.Select(h => h.Id).ToList();
        }
        else
        {
            var userHotels = await _hotelService.GetAllAsync();
            hotelIds = userHotels.Select(h => h.Id).ToList();
        }

        if (!hotelIds.Any())
        {
            return Ok(new
            {
                TotalRevenue = 0,
                RoomRevenue = 0,
                DepositRevenue = 0,
                CompletedReservations = 0,
                CancellationCount = 0,
                RevenueByRoomType = new List<object>(),
                RevenueByPaymentMethod = new List<object>(),
                RevenueByBookingType = new List<object>(),
                AverageDailyRate = 0,
                RevPAR = 0,
                TotalRoomNights = 0,
                PeriodStart = startDate ?? DateTime.UtcNow.AddMonths(-1),
                PeriodEnd = endDate ?? DateTime.UtcNow
            });
        }

        // Set default date range if not provided (last 30 days)
        var start = startDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var end = endDate ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
        
        // Prevent excessive date ranges (max 1 year)
        if ((end - start).Days > 365)
        {
            return BadRequest(new { message = "Date range cannot exceed 365 days" });
        }

        // Get only rooms for user's hotels (optimize by filtering upfront)
        var allRooms = await _roomService.GetAllAsync();
        var userRooms = allRooms.Where(r => hotelIds.Contains(r.HotelId)).ToList();
        var roomsDict = userRooms.ToDictionary(r => r.Id);
        
        // Get all reservations and filter to relevant ones
        var allReservations = await _reservationService.GetAllReservationsAsync();
        
        // Filter to only completed reservations in date range for user's hotels
        var completedReservations = allReservations
            .Where(r => hotelIds.Contains(r.HotelId) &&
                       r.CheckInDate >= start &&
                       r.CheckInDate <= end &&
                       r.Status == ReservationStatus.CheckedOut)
            .ToList();
        
        // Also get all reservations in period for cancellation count
        var relevantReservations = allReservations
            .Where(r => hotelIds.Contains(r.HotelId) &&
                       r.CheckInDate >= start &&
                       r.CheckInDate <= end)
            .ToList();

        // Calculate total revenue
        var totalRevenue = completedReservations.Sum(r => r.TotalAmount);
        var depositRevenue = completedReservations.Sum(r => r.DepositAmount);

        // Revenue by room type - get room type from Room entity
        var revenueByRoomType = completedReservations
            .Where(r => roomsDict.ContainsKey(r.RoomId))
            .GroupBy(r => roomsDict[r.RoomId].Type)
            .Select(g => new
            {
                RoomType = g.Key,
                RoomTypeName = g.Key.ToString(),
                Revenue = g.Sum(r => r.TotalAmount),
                Count = g.Count(),
                Percentage = totalRevenue > 0 ? Math.Round((decimal)((double)g.Sum(r => r.TotalAmount) / (double)totalRevenue) * 100, 1) : 0
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        // Revenue by payment method
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

        // Revenue by booking type
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

        // Count cancellations in period
        var cancellationCount = relevantReservations.Count(r => r.Status == ReservationStatus.Cancelled);

        // Calculate average daily rate (ADR) and RevPAR
        var totalRoomNights = completedReservations.Sum(r =>
        {
            if (r.BookingType == BookingType.ShortStay)
            {
                // For short stays, count as fraction of a day
                return (decimal)(r.DurationInHours ?? 0) / 24m;
            }
            else
            {
                // For overnight stays
                return (decimal)(r.CheckOutDate.Date - r.CheckInDate.Date).Days;
            }
        });

        var adr = totalRoomNights > 0 ? totalRevenue / totalRoomNights : 0;

        // Get total available room nights in period (userRooms already defined earlier)
        var daysInPeriod = (end.Date - start.Date).Days;
        var totalAvailableRoomNights = (decimal)(userRooms.Count * daysInPeriod);
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
