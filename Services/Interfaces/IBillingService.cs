using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Billing;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// An owner's billing: paying for a plan, changing or cancelling it, and applying what
/// the payment provider reports. Also renews and expires subscriptions on a schedule.
/// </summary>
public interface IBillingService
{
    Task<BillingOverviewDto> GetOverviewAsync(string ownerId);

    Task<CheckoutSession> StartCheckoutAsync(string ownerId, SubscriptionPlan plan, BillingInterval interval);

    /// <summary>Upgrades apply now; downgrades from the next renewal</summary>
    Task<BillingOverviewDto> ChangePlanAsync(string ownerId, SubscriptionPlan plan);

    /// <summary>The plan runs to the end of the paid period, then drops to Free</summary>
    Task<BillingOverviewDto> CancelAsync(string ownerId);

    Task<BillingOverviewDto> ResumeAsync(string ownerId);

    /// <summary>Verifies and applies a provider's webhook</summary>
    Task HandleWebhookAsync(string providerName, string body, string? signature);

    /// <summary>Applies a provider event once; false if it was already applied or doesn't apply</summary>
    Task<bool> ApplyEventAsync(BillingEvent billingEvent, BillingSource source);

    /// <summary>Charges due renewals and moves lapsed subscriptions to Free; returns how many changed</summary>
    Task<int> RunMaintenanceAsync();
}
