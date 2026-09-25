using HotelManagement.Data;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Billing;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

public class BillingService : IBillingService
{
    private readonly ApplicationDbContext _context;
    private readonly IEntitlementService _entitlements;
    private readonly IReadOnlyList<IBillingProvider> _providers;
    private readonly TimeProvider _time;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        ApplicationDbContext context,
        IEntitlementService entitlements,
        IEnumerable<IBillingProvider> providers,
        TimeProvider time,
        ILogger<BillingService> logger)
    {
        _context = context;
        _entitlements = entitlements;
        _providers = providers.ToList();
        _time = time;
        _logger = logger;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>The provider new checkouts go to (the fake one until a real one is added)</summary>
    private IBillingProvider CheckoutProvider => _providers[0];

    private IBillingProvider? ProviderFor(Subscription subscription) =>
        _providers.FirstOrDefault(p => p.Source == subscription.Source);

    private bool IsPayingAutomatically(Subscription subscription) =>
        subscription.RenewsAutomatically(Now) && ProviderFor(subscription) != null;

    #region Owner

    public async Task<BillingOverviewDto> GetOverviewAsync(string ownerId)
    {
        var subscription = await _entitlements.EnsureSubscriptionAsync(ownerId);
        var now = Now;
        var currentPlan = subscription.GetEffectivePlan(now);
        var usage = await _entitlements.GetUsageAsync(ownerId);

        var periodEnd = subscription.Status == SubscriptionStatus.PastDue ? subscription.GraceUntil : subscription.AccessUntil;
        var history = await _context.SubscriptionEvents
            .Where(e => e.SubscriptionId == subscription.Id)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Take(50)
            .Select(e => new SubscriptionEventDto
            {
                Type = e.Type,
                Plan = e.Plan,
                OldAccessUntil = e.OldAccessUntil,
                NewAccessUntil = e.NewAccessUntil,
                Amount = e.Amount,
                Reference = e.Reference,
                Reason = e.Reason,
                ActorName = e.Actor == null ? null : e.Actor.FirstName + " " + e.Actor.LastName,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        return new BillingOverviewDto
        {
            CurrentPlan = currentPlan,
            SubscribedPlan = subscription.Plan,
            Status = subscription.Status,
            Interval = subscription.Interval,
            Source = subscription.Source,
            AccessUntil = subscription.AccessUntil,
            TrialEndsAt = subscription.TrialEndsAt,
            GraceUntil = subscription.GraceUntil,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            ScheduledPlan = subscription.ScheduledPlan,
            DaysLeft = periodEnd > now && currentPlan != SubscriptionPlan.Free
                ? (int)Math.Ceiling((periodEnd.Value - now).TotalDays)
                : null,
            CanCheckout = !IsPayingAutomatically(subscription),
            Limits = PlanDto.From(PlanCatalog.Get(currentPlan)),
            Usage = new PlanUsageDto { Hotels = usage.Hotels, Rooms = usage.Rooms, Staff = usage.Staff },
            Plans = PlanCatalog.All.Select(PlanDto.From).ToList(),
            History = history
        };
    }

    public async Task<CheckoutSession> StartCheckoutAsync(string ownerId, SubscriptionPlan plan, BillingInterval interval)
    {
        if (plan == SubscriptionPlan.Free)
            throw new BusinessRuleException("The Free plan doesn't need a checkout");

        var subscription = await _entitlements.EnsureSubscriptionAsync(ownerId);
        if (IsPayingAutomatically(subscription))
            throw new BusinessRuleException($"You're already subscribed to {PlanCatalog.Get(subscription.Plan).Name}; change your plan instead");

        return CheckoutProvider.CreateCheckout(subscription, plan, interval);
    }

    private async Task<Subscription> GetPaidSubscriptionAsync(string ownerId)
    {
        var subscription = await _entitlements.EnsureSubscriptionAsync(ownerId);
        if (!IsPayingAutomatically(subscription))
            throw new BusinessRuleException("There's no paid subscription to change; choose a plan at checkout");
        return subscription;
    }

    public async Task<BillingOverviewDto> ChangePlanAsync(string ownerId, SubscriptionPlan plan)
    {
        var subscription = await GetPaidSubscriptionAsync(ownerId);
        if (plan == SubscriptionPlan.Free)
            throw new BusinessRuleException("To move to the Free plan, cancel your subscription");

        var now = Now;
        var from = PlanCatalog.Get(subscription.Plan).Name;
        if (plan == subscription.Plan)
        {
            // Choosing the current plan again undoes a scheduled downgrade
            if (subscription.ScheduledPlan == null)
                return await GetOverviewAsync(ownerId);
            subscription.ScheduledPlan = null;
            subscription.AddEvent(SubscriptionEventType.PlanChanged, now, subscription.AccessUntil, $"Staying on {from}", ownerId);
        }
        else if (plan > subscription.Plan)
        {
            await ProviderFor(subscription)!.ChangePlanAsync(subscription, plan);
            subscription.Plan = plan;
            subscription.ScheduledPlan = null;
            subscription.AddEvent(SubscriptionEventType.PlanChanged, now, subscription.AccessUntil, $"Upgraded from {from}", ownerId);
        }
        else
        {
            // Downgrades wait until the paid period ends, so nothing already paid for is lost
            subscription.ScheduledPlan = plan;
            subscription.AddEvent(SubscriptionEventType.PlanChanged, now, subscription.AccessUntil,
                $"Moves to {PlanCatalog.Get(plan).Name} at the next renewal", ownerId);
        }

        await _context.SaveChangesAsync();
        return await GetOverviewAsync(ownerId);
    }

    public async Task<BillingOverviewDto> CancelAsync(string ownerId)
    {
        var subscription = await GetPaidSubscriptionAsync(ownerId);
        if (!subscription.CancelAtPeriodEnd)
        {
            await ProviderFor(subscription)!.CancelAtPeriodEndAsync(subscription);
            subscription.CancelAtPeriodEnd = true;
            subscription.ScheduledPlan = null;
            subscription.AddEvent(SubscriptionEventType.CancelScheduled, Now, subscription.AccessUntil,
                "Ends at the end of the paid period", ownerId);
            await _context.SaveChangesAsync();
        }
        return await GetOverviewAsync(ownerId);
    }

    public async Task<BillingOverviewDto> ResumeAsync(string ownerId)
    {
        var subscription = await GetPaidSubscriptionAsync(ownerId);
        if (subscription.CancelAtPeriodEnd)
        {
            await ProviderFor(subscription)!.ResumeAsync(subscription);
            subscription.CancelAtPeriodEnd = false;
            subscription.AddEvent(SubscriptionEventType.CancelReverted, Now, subscription.AccessUntil,
                "Renews automatically again", ownerId);
            await _context.SaveChangesAsync();
        }
        return await GetOverviewAsync(ownerId);
    }

    #endregion

    #region Provider events

    public async Task HandleWebhookAsync(string providerName, string body, string? signature)
    {
        var provider = _providers.FirstOrDefault(p => p.Name == providerName)
            ?? throw new KeyNotFoundException($"Unknown payment provider '{providerName}'");

        await ApplyEventAsync(provider.ParseWebhook(body, signature), provider.Source);
    }

    public async Task<bool> ApplyEventAsync(BillingEvent billingEvent, BillingSource source)
    {
        if (await _context.ProcessedBillingEvents.AnyAsync(e => e.EventId == billingEvent.Id))
            return false;

        var subscription = await _entitlements.EnsureSubscriptionAsync(billingEvent.OwnerId, startTrial: false);
        var now = Now;
        var old = subscription.AccessUntil;
        var applied = true;

        switch (billingEvent.Type)
        {
            case BillingEventType.SubscriptionActivated:
                subscription.Plan = billingEvent.Plan;
                subscription.Interval = billingEvent.Interval;
                subscription.Source = source;
                subscription.Status = SubscriptionStatus.Active;
                subscription.AccessUntil = billingEvent.PeriodEnd;
                subscription.GraceUntil = null;
                subscription.CancelAtPeriodEnd = false;
                subscription.ScheduledPlan = null;
                subscription.ProviderSubscriptionId = billingEvent.ProviderSubscriptionId;
                subscription.ProviderCustomerId = billingEvent.ProviderCustomerId;
                subscription.AddEvent(SubscriptionEventType.Subscribed, now, old,
                    $"{PlanCatalog.Get(billingEvent.Plan).Name}, billed {billingEvent.Interval.ToString().ToLowerInvariant()}",
                    amount: billingEvent.Amount, reference: billingEvent.ProviderSubscriptionId);
                break;

            case BillingEventType.SubscriptionRenewed when IsCurrent(subscription, billingEvent):
                subscription.Plan = billingEvent.Plan;
                subscription.Status = SubscriptionStatus.Active;
                subscription.GraceUntil = null;
                subscription.ScheduledPlan = null;
                // Never shorten access, e.g. after a SuperAdmin extended it
                if (billingEvent.PeriodEnd > subscription.AccessUntil || subscription.AccessUntil == null)
                    subscription.AccessUntil = billingEvent.PeriodEnd;
                subscription.AddEvent(SubscriptionEventType.Renewed, now, old,
                    amount: billingEvent.Amount, reference: billingEvent.ProviderSubscriptionId);
                break;

            case BillingEventType.PaymentFailed when IsCurrent(subscription, billingEvent):
                subscription.Status = SubscriptionStatus.PastDue;
                subscription.GraceUntil = now.AddDays(PlanCatalog.GraceDays);
                subscription.AddEvent(SubscriptionEventType.PaymentFailed, now, old,
                    $"The plan keeps working for {PlanCatalog.GraceDays} days while the payment is sorted out");
                break;

            case BillingEventType.SubscriptionCanceled when IsCurrent(subscription, billingEvent):
                Expire(subscription, now, "Ended by the payment provider");
                break;

            default:
                // e.g. a late event about a subscription that has since been replaced
                _logger.LogWarning("Ignored billing event {EventId} ({Type}) for owner {OwnerId}",
                    billingEvent.Id, billingEvent.Type, billingEvent.OwnerId);
                applied = false;
                break;
        }

        _context.ProcessedBillingEvents.Add(new ProcessedBillingEvent
        {
            EventId = billingEvent.Id,
            Provider = source.ToString(),
            ProcessedAt = now
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException) when (_context.ChangeTracker.Entries<ProcessedBillingEvent>().Any())
        {
            // The same notification arrived twice at once; the other request applied it
            _logger.LogInformation("Billing event {EventId} was applied concurrently", billingEvent.Id);
            return false;
        }

        return applied;
    }

    private static bool IsCurrent(Subscription subscription, BillingEvent billingEvent) =>
        subscription.ProviderSubscriptionId != null && subscription.ProviderSubscriptionId == billingEvent.ProviderSubscriptionId;

    /// <summary>
    /// Back to Free. Nothing is deleted; the owner just can't add beyond Free's limits.
    /// </summary>
    private static void Expire(Subscription subscription, DateTime now, string reason)
    {
        var old = subscription.Status == SubscriptionStatus.PastDue ? subscription.GraceUntil : subscription.AccessUntil;
        subscription.Plan = SubscriptionPlan.Free;
        subscription.Status = SubscriptionStatus.Expired;
        subscription.Source = BillingSource.None;
        subscription.AccessUntil = null;
        subscription.GraceUntil = null;
        subscription.CancelAtPeriodEnd = false;
        subscription.ScheduledPlan = null;
        subscription.ProviderSubscriptionId = null;
        subscription.AddEvent(SubscriptionEventType.Expired, now, old, reason);
    }

    #endregion

    #region Maintenance

    public async Task<int> RunMaintenanceAsync()
    {
        var now = Now;
        var changed = 0;

        // 1. Renewals the provider charges when asked (the fake one); real providers send webhooks
        var due = await _context.Subscriptions
            .Where(s => s.Plan != SubscriptionPlan.Free
                && s.Status == SubscriptionStatus.Active
                && !s.CancelAtPeriodEnd
                && s.Source != BillingSource.None && s.Source != BillingSource.Manual
                && s.AccessUntil != null && s.AccessUntil <= now)
            .ToListAsync();

        foreach (var subscription in due)
        {
            var provider = ProviderFor(subscription);
            // Catch up on periods missed while the app was down, one renewal at a time
            for (var i = 0; provider != null && i < 24 && subscription.AccessUntil <= now && subscription.Status == SubscriptionStatus.Active; i++)
            {
                var renewal = await provider.CollectDueRenewalAsync(subscription, now);
                if (renewal == null || !await ApplyEventAsync(renewal, subscription.Source))
                    break;
                changed++;
            }
        }

        // 2. Trials, paid periods and grace periods that have run out
        var lapsed = await _context.Subscriptions
            .Where(s => s.Plan != SubscriptionPlan.Free
                && (((s.Status == SubscriptionStatus.Trialing || s.Status == SubscriptionStatus.Active) && s.AccessUntil != null && s.AccessUntil <= now)
                    || (s.Status == SubscriptionStatus.PastDue && (s.GraceUntil == null || s.GraceUntil <= now))))
            .ToListAsync();

        foreach (var subscription in lapsed)
        {
            var reason = subscription.Status switch
            {
                SubscriptionStatus.Trialing => "Trial ended",
                SubscriptionStatus.PastDue => "Payment wasn't received",
                _ when subscription.CancelAtPeriodEnd => "Cancelled",
                _ when subscription.Source == BillingSource.None => "Free access ended",
                _ => "Paid period ended"
            };
            Expire(subscription, now, reason);
            changed++;
        }

        if (lapsed.Count > 0)
            await _context.SaveChangesAsync();

        return changed;
    }

    #endregion
}
