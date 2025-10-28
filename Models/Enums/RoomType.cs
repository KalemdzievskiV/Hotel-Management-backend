namespace HotelManagement.Models.Enums;

/// <summary>
/// Room type classification
/// </summary>
public enum RoomType
{
    /// <summary>
    /// Single bed room (1 guest)
    /// </summary>
    Single = 1,
    
    /// <summary>
    /// Double bed room (2 guests)
    /// </summary>
    Double = 2,
    
    /// <summary>
    /// Twin beds room (2 guests)
    /// </summary>
    Twin = 3,
    
    /// <summary>
    /// Triple room (3 guests)
    /// </summary>
    Triple = 4,
    
    /// <summary>
    /// Suite with separate living area
    /// </summary>
    Suite = 5,
    
    /// <summary>
    /// Deluxe room with premium amenities
    /// </summary>
    Deluxe = 6,
    
    /// <summary>
    /// Presidential suite (highest tier)
    /// </summary>
    Presidential = 7,
    
    /// <summary>
    /// Studio apartment with kitchenette
    /// </summary>
    Studio = 8,
    
    /// <summary>
    /// Family room (4+ guests)
    /// </summary>
    Family = 9,
    
    /// <summary>
    /// Accessible room for guests with disabilities
    /// </summary>
    Accessible = 10
}
