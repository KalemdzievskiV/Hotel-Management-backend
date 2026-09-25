using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Models.Constants;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using Microsoft.Extensions.Options;

namespace HotelManagement.Services.Billing;

/// <summary>
/// A pretend checkout offered by the app while no real payment provider is chosen
/// </summary>
public record FakeCheckout(
    string SessionId,
    string OwnerId,
    SubscriptionPlan Plan,
    BillingInterval Interval,
    decimal Amount,
    string Currency,
    DateTime ExpiresAt);

/// <summary>
/// Stands in for a real payment provider. No money moves: the app's own "test checkout" page
/// approves or declines, and this class produces signed notifications in the same shape a
/// real provider's webhooks are translated into, so the rest of billing runs exactly as it will
/// with a real provider. Renewals are "charged" when the background job finds them due.
/// </summary>
public class FakeBillingProvider : IBillingProvider
{
    public const string ProviderName = "fake";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly byte[] _key;
    private readonly BillingOptions _options;
    private readonly TimeProvider _time;

    public FakeBillingProvider(IOptions<BillingOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;
        _key = string.IsNullOrEmpty(_options.FakeSigningKey)
            ? RandomNumberGenerator.GetBytes(32)
            : Encoding.UTF8.GetBytes(_options.FakeSigningKey);
    }

    public string Name => ProviderName;

    public BillingSource Source => BillingSource.Fake;

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public string Sign(string body) => Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(body)));

    private bool HasValidSignature(string body, string? signature) =>
        signature != null && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Sign(body)), Encoding.ASCII.GetBytes(signature.ToUpperInvariant()));

    public static DateTime AddInterval(DateTime from, BillingInterval interval) =>
        interval == BillingInterval.Yearly ? from.AddYears(1) : from.AddMonths(1);

    #region Checkout

    public CheckoutSession CreateCheckout(Subscription subscription, SubscriptionPlan plan, BillingInterval interval)
    {
        var checkout = new FakeCheckout(
            $"fake_chk_{Guid.NewGuid():N}",
            subscription.OwnerId,
            plan,
            interval,
            PlanCatalog.Get(plan).PriceFor(interval),
            PlanCatalog.Currency,
            Now.AddHours(1));

        var payload = Base64Url(JsonSerializer.Serialize(checkout, Json));
        return new CheckoutSession($"{_options.FakeCheckoutPath}?session={payload}.{Sign(payload)}");
    }

    /// <summary>
    /// Reads a checkout link made by CreateCheckout; refuses altered or expired links
    /// </summary>
    public FakeCheckout ReadCheckout(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2 || !HasValidSignature(parts[0], parts[1]))
            throw new BusinessRuleException("This checkout link isn't valid");

        FakeCheckout checkout;
        try
        {
            checkout = JsonSerializer.Deserialize<FakeCheckout>(FromBase64Url(parts[0]), Json)!;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new BusinessRuleException("This checkout link isn't valid");
        }

        if (checkout.ExpiresAt < Now)
            throw new BusinessRuleException("This checkout link has expired; start again from Billing");

        return checkout;
    }

    /// <summary>
    /// The owner approved the test payment: the notification a provider sends after checkout
    /// </summary>
    public (string Body, string Signature) CompleteCheckout(FakeCheckout checkout) =>
        CreateWebhook(new BillingEvent
        {
            Id = $"fake_evt_{Guid.NewGuid():N}",
            Type = BillingEventType.SubscriptionActivated,
            OwnerId = checkout.OwnerId,
            ProviderSubscriptionId = $"fake_sub_{Guid.NewGuid():N}",
            ProviderCustomerId = $"fake_cus_{checkout.OwnerId}",
            Plan = checkout.Plan,
            Interval = checkout.Interval,
            PeriodEnd = AddInterval(Now, checkout.Interval),
            Amount = checkout.Amount,
            Currency = checkout.Currency,
            OccurredAt = Now
        });

    #endregion

    #region Webhooks

    public (string Body, string Signature) CreateWebhook(BillingEvent billingEvent)
    {
        var body = JsonSerializer.Serialize(billingEvent, Json);
        return (body, Sign(body));
    }

    public BillingEvent ParseWebhook(string body, string? signature)
    {
        if (!HasValidSignature(body, signature))
            throw new UnauthorizedAccessException("Webhook signature doesn't match");

        return JsonSerializer.Deserialize<BillingEvent>(body, Json)
            ?? throw new ArgumentException("Empty webhook");
    }

    /// <summary>
    /// A renewal as the provider would report it: the next period, at the price of the plan
    /// that applies from then on
    /// </summary>
    public BillingEvent CreateRenewal(Subscription subscription, bool paid)
    {
        var plan = subscription.ScheduledPlan ?? subscription.Plan;
        var periodStart = subscription.AccessUntil ?? Now;
        return new BillingEvent
        {
            Id = $"fake_evt_{Guid.NewGuid():N}",
            Type = paid ? BillingEventType.SubscriptionRenewed : BillingEventType.PaymentFailed,
            OwnerId = subscription.OwnerId,
            ProviderSubscriptionId = subscription.ProviderSubscriptionId,
            Plan = plan,
            Interval = subscription.Interval,
            PeriodEnd = paid ? AddInterval(periodStart, subscription.Interval) : null,
            Amount = PlanCatalog.Get(plan).PriceFor(subscription.Interval),
            Currency = PlanCatalog.Currency,
            OccurredAt = Now
        };
    }

    #endregion

    #region Subscription changes

    // The Subscription record is this provider's only state, and BillingService updates it,
    // so there is nothing more to do here. A real provider calls its API in these methods.

    public Task CancelAtPeriodEndAsync(Subscription subscription) => Task.CompletedTask;

    public Task ResumeAsync(Subscription subscription) => Task.CompletedTask;

    public Task ChangePlanAsync(Subscription subscription, SubscriptionPlan plan) => Task.CompletedTask;

    public Task ExtendAsync(Subscription subscription, DateTime nextChargeAt) => Task.CompletedTask;

    public Task<BillingEvent?> CollectDueRenewalAsync(Subscription subscription, DateTime now) =>
        Task.FromResult<BillingEvent?>(subscription.AccessUntil <= now ? CreateRenewal(subscription, paid: true) : null);

    #endregion

    private static string Base64Url(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string FromBase64Url(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
