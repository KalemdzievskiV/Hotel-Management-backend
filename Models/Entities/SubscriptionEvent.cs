using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

/// <summary>
/// One entry in a subscription's history: what changed, who did it and why
/// </summary>
public class SubscriptionEvent
{
    public int Id { get; set; }

    public int SubscriptionId { get; set; }

    [ForeignKey("SubscriptionId")]
    public Subscription Subscription { get; set; } = null!;

    public SubscriptionEventType Type { get; set; }

    /// <summary>
    /// The plan after the change
    /// </summary>
    public SubscriptionPlan Plan { get; set; }

    public DateTime? OldAccessUntil { get; set; }

    public DateTime? NewAccessUntil { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Amount { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// Who made the change; null for the payment provider and the background job
    /// </summary>
    [MaxLength(450)]
    public string? ActorUserId { get; set; }

    [ForeignKey("ActorUserId")]
    public ApplicationUser? Actor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
