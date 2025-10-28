namespace HotelManagement.Models.Enums;

/// <summary>
/// Type of booking (daily vs short-stay)
/// </summary>
public enum BookingType
{
    /// <summary>
    /// Traditional overnight stay (one or more nights)
    /// </summary>
    Daily = 0,
    
    /// <summary>
    /// Short-stay / hourly booking (few hours)
    /// </summary>
    ShortStay = 1
}
