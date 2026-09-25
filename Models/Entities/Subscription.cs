using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

/// <summary>
/// A hotel owner's (Admin's) subscription. It covers all hotels they own and the staff of
/// those hotels. This record, not the payment provider, decides what the owner can use;
/// providers only report payments into it.
/// </summary>
public class Subscription
{
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    [ForeignKey("OwnerId")]
    public ApplicationUser Owner { get; set; } = null!;

    /// <summary>
    /// The plan being trialled or paid for; Free once a trial or paid period has ended
    /// </summary>
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public BillingInterval Interval { get; set; } = BillingInterval.Monthly;

    public BillingSource Source { get; set; } = BillingSource.None;

    /// <summary>
    /// End of the trial or of the paid period; null for the Free plan
    /// </summary>
    public DateTime? AccessUntil { get; set; }

    /// <summary>
    /// When the trial ends (kept for history after the owner subscribes)
    /// </summary>
    public DateTime? TrialEndsAt { get; set; }

    /// <summary>
    /// After a failed renewal, how long the plan keeps working
    /// </summary>
    public DateTime? GraceUntil { get; set; }

    /// <summary>
    /// The owner cancelled: the plan runs to AccessUntil and then drops to Free
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>
    /// A plan change the owner asked for, applied at the next renewal (downgrades)
    /// </summary>
    public SubscriptionPlan? ScheduledPlan { get; set; }

    [MaxLength(200)]
    public string? ProviderCustomerId { get; set; }

    [MaxLength(200)]
    public string? ProviderSubscriptionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<SubscriptionEvent> Events { get; set; } = new List<SubscriptionEvent>();

    /// <summary>
    /// The plan the owner gets right now. Dates count even before the background job
    /// has tidied up the status, so an expired trial never keeps working.
    /// </summary>
    public SubscriptionPlan GetEffectivePlan(DateTime now) => Plan == SubscriptionPlan.Free
        ? SubscriptionPlan.Free
        : Status switch
        {
            SubscriptionStatus.Trialing or SubscriptionStatus.Active
                when AccessUntil == null || AccessUntil > now => Plan,
            SubscriptionStatus.PastDue when GraceUntil > now => Plan,
            _ => SubscriptionPlan.Free
        };

    /// <summary>
    /// Paid up through a payment provider that charges renewals by itself
    /// (not a trial, free access, or a bank transfer recorded by a SuperAdmin)
    /// </summary>
    public bool RenewsAutomatically(DateTime now) =>
        Source is not (BillingSource.None or BillingSource.Manual)
        && Status == SubscriptionStatus.Active
        && GetEffectivePlan(now) != SubscriptionPlan.Free;

    /// <summary>
    /// Records a change in the history, after the subscription has been updated
    /// </summary>
    public void AddEvent(SubscriptionEventType type, DateTime now, DateTime? oldAccessUntil,
        string? reason = null, string? actorId = null, decimal? amount = null, string? reference = null)
    {
        Events.Add(new SubscriptionEvent
        {
            Type = type,
            Plan = Plan,
            OldAccessUntil = oldAccessUntil,
            NewAccessUntil = Status == SubscriptionStatus.PastDue ? GraceUntil : AccessUntil,
            Reason = reason,
            ActorUserId = actorId,
            Amount = amount,
            Reference = reference,
            CreatedAt = now
        });
        UpdatedAt = now;
    }
}
