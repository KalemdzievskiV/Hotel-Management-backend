using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// Service interface for Room-specific operations
/// </summary>
public interface IRoomService : ICrudService<RoomDto>
{
    /// <summary>
    /// Get all rooms for a specific hotel
    /// </summary>
    Task<IEnumerable<RoomDto>> GetRoomsByHotelIdAsync(int hotelId);
    
    /// <summary>
    /// Get rooms by hotel and status
    /// </summary>
    Task<IEnumerable<RoomDto>> GetRoomsByHotelAndStatusAsync(int hotelId, RoomStatus status);
    
    /// <summary>
    /// Get available rooms for a hotel (Active and Available status)
    /// </summary>
    Task<IEnumerable<RoomDto>> GetAvailableRoomsByHotelAsync(int hotelId);
    
    /// <summary>
    /// Update room status (e.g., for cleaning, maintenance)
    /// </summary>
    Task<RoomDto> UpdateRoomStatusAsync(int roomId, RoomStatus newStatus);
    
    /// <summary>
    /// Check if a room number is unique within a hotel
    /// </summary>
    Task<bool> IsRoomNumberUniqueAsync(int hotelId, string roomNumber, int? excludeRoomId = null);
    
    /// <summary>
    /// Mark room as cleaned
    /// </summary>
    Task MarkRoomAsCleanedAsync(int roomId);
    
    /// <summary>
    /// Record maintenance for a room
    /// </summary>
    Task RecordMaintenanceAsync(int roomId, string notes);
}
