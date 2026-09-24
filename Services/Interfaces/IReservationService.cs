using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// Service interface for Reservation management operations
/// </summary>
public interface IReservationService
{
    // CRUD Operations
    Task<ReservationDto> CreateReservationAsync(CreateReservationDto createDto);
    Task<ReservationDto?> GetReservationByIdAsync(int id);
    Task<IEnumerable<ReservationDto>> GetReservationsForHotelsAsync(IReadOnlyCollection<int> hotelIds);
    Task<ReservationDto> UpdateReservationAsync(int id, UpdateReservationDto updateDto);
    Task DeleteReservationAsync(int id);
    
    // Query Operations
    Task<IEnumerable<ReservationDto>> GetReservationsByHotelAsync(int hotelId);
    Task<IEnumerable<ReservationDto>> GetReservationsByRoomAsync(int roomId);
    Task<IEnumerable<ReservationDto>> GetReservationsByGuestAsync(int guestId, IReadOnlyCollection<int> hotelIds);
    Task<IEnumerable<ReservationDto>> GetReservationsByStatusAsync(ReservationStatus status, IReadOnlyCollection<int> hotelIds);
    Task<IEnumerable<ReservationDto>> GetReservationsByDateRangeAsync(DateTime startDate, DateTime endDate, IReadOnlyCollection<int> hotelIds);
    Task<IEnumerable<ReservationDto>> GetGuestUserReservationsAsync(string userId);
    Task<IEnumerable<ReservationDto>> GetCheckInsOnAsync(DateTime day, IReadOnlyCollection<int> hotelIds);
    Task<IEnumerable<ReservationDto>> GetCheckOutsOnAsync(DateTime day, IReadOnlyCollection<int> hotelIds);
    
    // Room Availability
    Task<bool> IsRoomAvailableAsync(int roomId, DateTime checkIn, DateTime checkOut, int? excludeReservationId = null);
    Task<IEnumerable<ReservationDto>> GetConflictingReservationsAsync(int roomId, DateTime checkIn, DateTime checkOut);
    Task<IEnumerable<RoomDto>> GetAvailableRoomsAsync(int hotelId, DateTime checkIn, DateTime checkOut, BookingType bookingType, int? minCapacity = null, string? roomType = null);
    
    // Status Management
    Task<ReservationDto> ConfirmReservationAsync(int id);
    Task<ReservationDto> CheckInReservationAsync(int id);
    Task<ReservationDto> CheckOutReservationAsync(int id);
    Task<ReservationDto> CancelReservationAsync(int id, string reason);
    Task<ReservationDto> MarkAsNoShowAsync(int id);
    
    // Payment
    Task<ReservationDto> RecordPaymentAsync(int id, decimal amount, PaymentMethod paymentMethod, string? reference = null);
    Task<ReservationDto> RecordRefundAsync(int id, decimal amount, string? reason = null);
    Task<IEnumerable<PaymentDto>> GetPaymentsAsync(int reservationId);

    /// <summary>Sets a discount off the room price, or an override price expressed as one</summary>
    Task<ReservationDto> ApplyPriceAdjustmentAsync(int id, decimal discountAmount, string? reason, decimal? overridePrice);
    Task<ReservationDto> AddExtraChargesAsync(int id, decimal amount, string? notes);
    
    // Statistics
    Task<int> GetTotalReservationsCountAsync(IReadOnlyCollection<int> hotelIds);
    Task<decimal> GetTotalRevenueAsync(IReadOnlyCollection<int> hotelIds);
    Task<Dictionary<ReservationStatus, int>> GetReservationCountByStatusAsync(IReadOnlyCollection<int> hotelIds);
    Task<Dictionary<string, int>> GetReservationCountByMonthAsync(int year, IReadOnlyCollection<int> hotelIds);
}
