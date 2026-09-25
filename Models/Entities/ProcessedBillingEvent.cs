using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.Entities;

/// <summary>
/// A payment-provider notification that has been applied. Providers resend notifications,
/// so each one is recorded to make sure it only takes effect once.
/// </summary>
public class ProcessedBillingEvent
{
    [Key]
    [MaxLength(200)]
    public string EventId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
