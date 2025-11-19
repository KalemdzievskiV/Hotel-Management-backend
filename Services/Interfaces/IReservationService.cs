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
    Task<IEnumerable<ReservationDto>> GetAllReservationsAsync();
    Task<ReservationDto> UpdateReservationAsync(int id, UpdateReservationDto updateDto);
    Task DeleteReservationAsync(int id);
    
    // Query Operations
    Task<IEnumerable<ReservationDto>> GetReservationsByHotelAsync(int hotelId);
    Task<IEnumerable<ReservationDto>> GetReservationsByRoomAsync(int roomId);
    Task<IEnumerable<ReservationDto>> GetReservationsByGuestAsync(int guestId);
    Task<IEnumerable<ReservationDto>> GetReservationsByStatusAsync(ReservationStatus status);
    Task<IEnumerable<ReservationDto>> GetReservationsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<ReservationDto>> GetUserReservationsAsync(string userId);
    
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
    
    // Statistics
    Task<int> GetTotalReservationsCountAsync();
    Task<decimal> GetTotalRevenueAsync();
    Task<Dictionary<ReservationStatus, int>> GetReservationCountByStatusAsync();
    Task<Dictionary<string, int>> GetReservationCountByMonthAsync(int year);
}
