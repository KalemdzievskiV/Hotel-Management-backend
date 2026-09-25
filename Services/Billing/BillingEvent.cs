using System.Text.Json.Serialization;
using HotelManagement.Models.Enums;

namespace HotelManagement.Services.Billing;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingEventType
{
    /// <summary>The owner paid at checkout; the subscription starts</summary>
    SubscriptionActivated,

    /// <summary>A renewal was paid</summary>
    SubscriptionRenewed,

    /// <summary>A renewal payment failed</summary>
    PaymentFailed,

    /// <summary>The provider ended the subscription (e.g. after failed retries)</summary>
    SubscriptionCanceled
}

/// <summary>
/// A payment-provider notification, translated into the app's terms. Each provider parses its
/// own webhook format into this, so BillingService handles every provider the same way.
/// </summary>
public record BillingEvent
{
    /// <summary>The provider's id for this notification, used to apply it only once</summary>
    public required string Id { get; init; }

    public required BillingEventType Type { get; init; }

    /// <summary>Whose subscription (passed through checkout, like Paddle's custom_data)</summary>
    public required string OwnerId { get; init; }

    public string? ProviderSubscriptionId { get; init; }

    public string? ProviderCustomerId { get; init; }

    public SubscriptionPlan Plan { get; init; }

    public BillingInterval Interval { get; init; }

    /// <summary>End of the period that was paid for</summary>
    public DateTime? PeriodEnd { get; init; }

    public decimal? Amount { get; init; }

    public string? Currency { get; init; }

    public DateTime OccurredAt { get; init; }
}
