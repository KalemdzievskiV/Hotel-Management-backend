using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;

namespace HotelManagement.Services.Billing;

/// <summary>
/// Where the owner is sent to pay: the provider's hosted checkout (or the fake one)
/// </summary>
public record CheckoutSession(string CheckoutUrl);

/// <summary>
/// A payment provider. Everything provider-specific lives behind this interface, so choosing a
/// real provider (e.g. Paddle) means adding one implementation. The provider only takes money;
/// the Subscription record decides access, and BillingService applies the provider's events to it.
/// </summary>
public interface IBillingProvider
{
    /// <summary>Name used in the webhook URL (/api/billing/webhooks/{name})</summary>
    string Name { get; }

    BillingSource Source { get; }

    /// <summary>Starts a hosted checkout for the owner to pay for a plan</summary>
    CheckoutSession CreateCheckout(Subscription subscription, SubscriptionPlan plan, BillingInterval interval);

    /// <summary>
    /// Checks a webhook's signature and translates it. Throws UnauthorizedAccessException
    /// when the signature doesn't match.
    /// </summary>
    BillingEvent ParseWebhook(string body, string? signature);

    /// <summary>Stops automatic renewal at the end of the paid period</summary>
    Task CancelAtPeriodEndAsync(Subscription subscription);

    /// <summary>Undoes a scheduled cancellation</summary>
    Task ResumeAsync(Subscription subscription);

    /// <summary>Switches the plan that is charged from now on</summary>
    Task ChangePlanAsync(Subscription subscription, SubscriptionPlan plan);

    /// <summary>Moves the next charge later without charging for the extra time</summary>
    Task ExtendAsync(Subscription subscription, DateTime nextChargeAt);

    /// <summary>
    /// Charges a renewal that is due. Real providers renew by themselves and send a webhook,
    /// so they return null; the fake provider charges here, when the background job asks.
    /// </summary>
    Task<BillingEvent?> CollectDueRenewalAsync(Subscription subscription, DateTime now);
}
