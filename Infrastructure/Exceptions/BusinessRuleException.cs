namespace HotelManagement.Infrastructure.Exceptions;

/// <summary>
/// A request broke a business rule (room not available, invalid amount, wrong status, ...).
/// Returned to the client as 400 with its message. Other exceptions are treated as bugs (500).
/// Derives from InvalidOperationException so existing callers catching that still work.
/// </summary>
public class BusinessRuleException : InvalidOperationException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
