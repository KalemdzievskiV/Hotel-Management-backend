namespace HotelManagement.Models.Enums;

/// <summary>
/// Payment status of a reservation
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// No payment received yet
    /// </summary>
    Unpaid = 0,
    
    /// <summary>
    /// Partial payment received (deposit)
    /// </summary>
    PartiallyPaid = 1,
    
    /// <summary>
    /// Full payment received
    /// </summary>
    Paid = 2,
    
    /// <summary>
    /// Refund in progress
    /// </summary>
    Refunding = 3,
    
    /// <summary>
    /// Refund completed
    /// </summary>
    Refunded = 4
}
