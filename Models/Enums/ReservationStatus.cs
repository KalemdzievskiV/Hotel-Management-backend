namespace HotelManagement.Models.Enums;

/// <summary>
/// Status of a reservation
/// </summary>
public enum ReservationStatus
{
    /// <summary>
    /// Reservation created but not yet confirmed
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Reservation confirmed and active
    /// </summary>
    Confirmed = 1,
    
    /// <summary>
    /// Guest has checked in
    /// </summary>
    CheckedIn = 2,
    
    /// <summary>
    /// Guest has checked out, reservation complete
    /// </summary>
    CheckedOut = 3,
    
    /// <summary>
    /// Reservation was cancelled
    /// </summary>
    Cancelled = 4,
    
    /// <summary>
    /// Guest did not show up (no-show)
    /// </summary>
    NoShow = 5
}
