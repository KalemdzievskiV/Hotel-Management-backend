namespace HotelManagement.Models.Enums;

/// <summary>
/// Current status of a room
/// </summary>
public enum RoomStatus
{
    /// <summary>
    /// Room is available for booking
    /// </summary>
    Available = 1,
    
    /// <summary>
    /// Room is currently occupied by a guest
    /// </summary>
    Occupied = 2,
    
    /// <summary>
    /// Room is being cleaned/prepared
    /// </summary>
    Cleaning = 3,
    
    /// <summary>
    /// Room is under maintenance/repair
    /// </summary>
    Maintenance = 4,
    
    /// <summary>
    /// Room is temporarily out of service
    /// </summary>
    OutOfService = 5,
    
    /// <summary>
    /// Room is reserved for upcoming reservation
    /// </summary>
    Reserved = 6
}
