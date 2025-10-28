namespace HotelManagement.Models.Enums;

/// <summary>
/// Method of payment for a reservation
/// </summary>
public enum PaymentMethod
{
    /// <summary>
    /// Cash payment
    /// </summary>
    Cash = 0,
    
    /// <summary>
    /// Credit card payment
    /// </summary>
    CreditCard = 1,
    
    /// <summary>
    /// Debit card payment
    /// </summary>
    DebitCard = 2,
    
    /// <summary>
    /// Bank transfer
    /// </summary>
    BankTransfer = 3,
    
    /// <summary>
    /// Online payment (PayPal, Stripe, etc.)
    /// </summary>
    Online = 4,
    
    /// <summary>
    /// Payment on arrival
    /// </summary>
    PayOnArrival = 5
}
