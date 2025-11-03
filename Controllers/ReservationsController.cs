using System.Security.Claims;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
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

    public ReservationsController(IReservationService reservationService, IHotelService hotelService)
    {
        _reservationService = reservationService;
        _hotelService = hotelService;
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
    /// Get reservation by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetReservationById(int id)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id);
        return reservation == null ? NotFound() : Ok(reservation);
    }

    /// <summary>
    /// Get all reservations (Admin/Manager only)
    /// Filters by user's hotels for non-SuperAdmin users
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetAllReservations()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSuperAdmin = User.IsInRole(AppRoles.SuperAdmin);
        
        if (isSuperAdmin)
        {
            // SuperAdmin sees all reservations
            var reservations = await _reservationService.GetAllReservationsAsync();
            return Ok(reservations);
        }
        
        // Regular admin/manager sees only reservations from their hotels
        var userHotels = await _hotelService.GetAllHotelsForUserAsync(userId!, isSuperAdmin: false);
        var hotelIds = userHotels.Select(h => h.Id).ToList();
        
        var allReservations = await _reservationService.GetAllReservationsAsync();
        var filteredReservations = allReservations.Where(r => hotelIds.Contains(r.HotelId));
        
        return Ok(filteredReservations);
    }

    /// <summary>
    /// Update a reservation
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateReservation(int id, [FromBody] UpdateReservationDto updateDto)
    {
        var reservation = await _reservationService.UpdateReservationAsync(id, updateDto);
        return Ok(reservation);
    }

    /// <summary>
    /// Delete a reservation (Admin only - only pending reservations)
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
    public async Task<IActionResult> DeleteReservation(int id)
    {
        await _reservationService.DeleteReservationAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Get reservations by hotel
    /// </summary>
    [HttpGet("hotel/{hotelId:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetReservationsByHotel(int hotelId)
    {
        var reservations = await _reservationService.GetReservationsByHotelAsync(hotelId);
        return Ok(reservations);
    }

    /// <summary>
    /// Get reservations by room
    /// </summary>
    [HttpGet("room/{roomId:int}")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
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
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
    public async Task<IActionResult> GetReservationsByStatus(ReservationStatus status)
    {
        var reservations = await _reservationService.GetReservationsByStatusAsync(status);
        return Ok(reservations);
    }

    /// <summary>
    /// Get reservations by date range
    /// </summary>
    [HttpGet("daterange")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
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
