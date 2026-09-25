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

public class SubscriptionAdminService : ISubscriptionAdminService
{
    private const int TrialEndingSoonDays = 7;

    private readonly ApplicationDbContext _context;
    private readonly IBillingService _billing;
    private readonly IReadOnlyList<IBillingProvider> _providers;
    private readonly TimeProvider _time;

    public SubscriptionAdminService(ApplicationDbContext context, IBillingService billing, IEnumerable<IBillingProvider> providers, TimeProvider time)
    {
        _context = context;
        _billing = billing;
        _providers = providers.ToList();
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    #region Reading

    private static decimal MonthlyRevenue(Subscription subscription, DateTime now)
    {
        var plan = subscription.GetEffectivePlan(now);
        var paying = subscription.Source is not BillingSource.None
            && subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue
            && plan != SubscriptionPlan.Free;
        if (!paying)
            return 0;

        var definition = PlanCatalog.Get(plan);
        return subscription.Interval == BillingInterval.Yearly && subscription.Source != BillingSource.Manual
            ? Math.Round(definition.YearlyPrice / 12, 2)
            : definition.MonthlyPrice;
    }

    private static bool Matches(Subscription subscription, SubscriptionFilter filter, DateTime now) => filter switch
    {
        SubscriptionFilter.Trialing => subscription.Status == SubscriptionStatus.Trialing,
        SubscriptionFilter.TrialEndingSoon => subscription.Status == SubscriptionStatus.Trialing
            && subscription.AccessUntil <= now.AddDays(TrialEndingSoonDays),
        SubscriptionFilter.Paying => MonthlyRevenue(subscription, now) > 0 && subscription.Status == SubscriptionStatus.Active,
        SubscriptionFilter.PastDue => subscription.Status == SubscriptionStatus.PastDue,
        SubscriptionFilter.Manual => subscription.Source == BillingSource.Manual,
        SubscriptionFilter.Free => subscription.GetEffectivePlan(now) == SubscriptionPlan.Free,
        _ => true
    };

    private async Task<List<SubscriptionSummaryDto>> SummarizeAsync(List<Subscription> subscriptions)
    {
        var ownerIds = subscriptions.Select(s => s.OwnerId).ToList();
        var hotels = await _context.Hotels
            .Where(h => ownerIds.Contains(h.OwnerId))
            .GroupBy(h => h.OwnerId)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OwnerId, x => x.Count);
        var rooms = await _context.Rooms
            .Where(r => ownerIds.Contains(r.Hotel.OwnerId))
            .GroupBy(r => r.Hotel.OwnerId)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OwnerId, x => x.Count);

        var now = Now;
        return subscriptions.Select(s =>
        {
            var currentPlan = s.GetEffectivePlan(now);
            var periodEnd = s.Status == SubscriptionStatus.PastDue ? s.GraceUntil : s.AccessUntil;
            return new SubscriptionSummaryDto
            {
                OwnerId = s.OwnerId,
                OwnerName = $"{s.Owner.FirstName} {s.Owner.LastName}",
                OwnerEmail = s.Owner.Email ?? string.Empty,
                OwnerActive = s.Owner.IsActive,
                Hotels = hotels.GetValueOrDefault(s.OwnerId),
                Rooms = rooms.GetValueOrDefault(s.OwnerId),
                CurrentPlan = currentPlan,
                SubscribedPlan = s.Plan,
                Status = s.Status,
                Source = s.Source,
                Interval = s.Interval,
                AccessUntil = s.AccessUntil,
                GraceUntil = s.GraceUntil,
                CancelAtPeriodEnd = s.CancelAtPeriodEnd,
                DaysLeft = periodEnd > now && currentPlan != SubscriptionPlan.Free
                    ? (int)Math.Ceiling((periodEnd.Value - now).TotalDays)
                    : null,
                MonthlyRevenue = MonthlyRevenue(s, now),
                CreatedAt = s.CreatedAt
            };
        }).ToList();
    }

    public async Task<List<SubscriptionSummaryDto>> ListAsync(SubscriptionFilter filter, string? search)
    {
        var query = _context.Subscriptions.Include(s => s.Owner).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s => (s.Owner.FirstName + " " + s.Owner.LastName).ToLower().Contains(term)
                || (s.Owner.Email != null && s.Owner.Email.ToLower().Contains(term)));
        }

        var now = Now;
        var subscriptions = (await query.ToListAsync())
            .Where(s => Matches(s, filter, now))
            // Most urgent first: soonest to run out
            .OrderBy(s => s.GetEffectivePlan(now) == SubscriptionPlan.Free)
            .ThenBy(s => s.Status == SubscriptionStatus.PastDue ? s.GraceUntil : s.AccessUntil)
            .ToList();

        return await SummarizeAsync(subscriptions);
    }

    public async Task<SubscriptionStatsDto> GetStatsAsync()
    {
        var now = Now;
        var subscriptions = await _context.Subscriptions.ToListAsync();
        return new SubscriptionStatsDto
        {
            Owners = subscriptions.Count,
            Trialing = subscriptions.Count(s => Matches(s, SubscriptionFilter.Trialing, now)),
            TrialsEndingSoon = subscriptions.Count(s => Matches(s, SubscriptionFilter.TrialEndingSoon, now)),
            Paying = subscriptions.Count(s => Matches(s, SubscriptionFilter.Paying, now)),
            PastDue = subscriptions.Count(s => Matches(s, SubscriptionFilter.PastDue, now)),
            Free = subscriptions.Count(s => Matches(s, SubscriptionFilter.Free, now)),
            MonthlyRevenue = subscriptions.Sum(s => MonthlyRevenue(s, now)),
            Currency = PlanCatalog.Currency
        };
    }

    public async Task<SubscriptionDetailDto> GetAsync(string ownerId)
    {
        var subscription = await FindAsync(ownerId);
        return new SubscriptionDetailDto
        {
            Summary = (await SummarizeAsync(new List<Subscription> { subscription })).Single(),
            Billing = await _billing.GetOverviewAsync(ownerId)
        };
    }

    private async Task<Subscription> FindAsync(string ownerId) =>
        await _context.Subscriptions.Include(s => s.Owner).FirstOrDefaultAsync(s => s.OwnerId == ownerId)
        ?? throw new KeyNotFoundException("This user has no subscription (only hotel owners have one)");

    #endregion

    #region Changes

    /// <summary>
    /// A new end date must be in the future and at most MaxAdminExtensionDays past the current end
    /// </summary>
    private DateTime CheckUntil(DateTime until, DateTime? currentEnd)
    {
        var now = Now;
        var from = currentEnd > now ? currentEnd.Value : now;
        if (until <= from)
            throw new BusinessRuleException($"Choose a date after {from:d MMM yyyy}");
        if ((until - from).TotalDays > PlanCatalog.MaxAdminExtensionDays)
            throw new BusinessRuleException($"One change can add at most {PlanCatalog.MaxAdminExtensionDays} days");
        return until;
    }

    private void RefuseWhileRenewingAutomatically(Subscription subscription, string action)
    {
        if (subscription.RenewsAutomatically(Now))
            throw new BusinessRuleException(
                $"This owner pays by card and renews automatically; extend or change their subscription instead of {action}");
    }

    private async Task<SubscriptionDetailDto> SaveAsync(Subscription subscription)
    {
        await _context.SaveChangesAsync();
        return await GetAsync(subscription.OwnerId);
    }

    public async Task<SubscriptionDetailDto> ExtendAsync(string ownerId, ExtendSubscriptionRequest request, string actorId)
    {
        var subscription = await FindAsync(ownerId);
        var now = Now;

        if (subscription.Status == SubscriptionStatus.PastDue)
            throw new BusinessRuleException("The last payment failed; give a grace period instead");
        if (subscription.GetEffectivePlan(now) == SubscriptionPlan.Free)
            throw new BusinessRuleException("This owner is on the Free plan; grant access or record a payment instead");
        if (request.Days == null && request.Until == null)
            throw new BusinessRuleException("Give a number of days or a date");

        var currentEnd = subscription.AccessUntil ?? now;
        var until = CheckUntil(request.Until ?? currentEnd.AddDays(request.Days!.Value), currentEnd);

        // For card payers, the provider moves the next charge too, without charging for the extra time
        var provider = _providers.FirstOrDefault(p => p.Source == subscription.Source);
        if (provider != null)
            await provider.ExtendAsync(subscription, until);

        var old = subscription.AccessUntil;
        subscription.AccessUntil = until;
        var isTrial = subscription.Status == SubscriptionStatus.Trialing;
        if (isTrial)
            subscription.TrialEndsAt = until;

        subscription.AddEvent(isTrial ? SubscriptionEventType.TrialExtended : SubscriptionEventType.Extended,
            now, old, request.Reason, actorId);
        return await SaveAsync(subscription);
    }

    public async Task<SubscriptionDetailDto> RecordManualPaymentAsync(string ownerId, ManualPaymentRequest request, string actorId)
    {
        var subscription = await FindAsync(ownerId);
        RefuseWhileRenewingAutomatically(subscription, "recording a bank transfer");
        if (request.Plan == SubscriptionPlan.Free)
            throw new BusinessRuleException("Choose the plan that was paid for");

        // A further payment for the same plan continues from the end of the current one
        var continues = subscription.Source == BillingSource.Manual && subscription.Plan == request.Plan
            && subscription.GetEffectivePlan(Now) == request.Plan;
        var until = CheckUntil(request.Until, continues ? subscription.AccessUntil : null);

        var old = subscription.AccessUntil;
        subscription.Plan = request.Plan;
        subscription.Status = SubscriptionStatus.Active;
        subscription.Source = BillingSource.Manual;
        subscription.AccessUntil = until;
        subscription.GraceUntil = null;
        subscription.CancelAtPeriodEnd = false;
        subscription.ScheduledPlan = null;
        subscription.ProviderSubscriptionId = null;
        subscription.AddEvent(SubscriptionEventType.ManualPayment, Now, old, request.Reason, actorId, request.Amount, request.Reference);
        return await SaveAsync(subscription);
    }

    public async Task<SubscriptionDetailDto> GrantAccessAsync(string ownerId, GrantAccessRequest request, string actorId)
    {
        var subscription = await FindAsync(ownerId);
        RefuseWhileRenewingAutomatically(subscription, "granting free access");
        if (request.Plan == SubscriptionPlan.Free)
            throw new BusinessRuleException("Choose a paid plan to grant");

        var until = CheckUntil(request.Until, null);
        var old = subscription.AccessUntil;
        subscription.Plan = request.Plan;
        subscription.Status = SubscriptionStatus.Active;
        subscription.Source = BillingSource.None;
        subscription.AccessUntil = until;
        subscription.GraceUntil = null;
        subscription.CancelAtPeriodEnd = false;
        subscription.ScheduledPlan = null;
        subscription.ProviderSubscriptionId = null;
        subscription.AddEvent(SubscriptionEventType.AccessGranted, Now, old, request.Reason, actorId);
        return await SaveAsync(subscription);
    }

    public async Task<SubscriptionDetailDto> ChangePlanAsync(string ownerId, AdminChangePlanRequest request, string actorId)
    {
        var subscription = await FindAsync(ownerId);
        if (request.Plan == SubscriptionPlan.Free)
            throw new BusinessRuleException("Choose a paid plan");
        if (subscription.GetEffectivePlan(Now) == SubscriptionPlan.Free)
            throw new BusinessRuleException("This owner is on the Free plan; grant access or record a payment instead");
        if (request.Plan == subscription.Plan && subscription.ScheduledPlan == null)
            return await GetAsync(ownerId);

        // Card payers are charged the new plan's price from their next renewal
        var provider = _providers.FirstOrDefault(p => p.Source == subscription.Source);
        if (provider != null)
            await provider.ChangePlanAsync(subscription, request.Plan);

        var from = PlanCatalog.Get(subscription.Plan).Name;
        subscription.Plan = request.Plan;
        subscription.ScheduledPlan = null;
        subscription.AddEvent(SubscriptionEventType.PlanChanged, Now, subscription.AccessUntil,
            $"From {from}: {request.Reason}", actorId);
        return await SaveAsync(subscription);
    }

    public async Task<SubscriptionDetailDto> GrantGraceAsync(string ownerId, GrantGraceRequest request, string actorId)
    {
        var subscription = await FindAsync(ownerId);
        if (subscription.Status != SubscriptionStatus.PastDue)
            throw new BusinessRuleException("A grace period is only for owners whose payment failed");

        var now = Now;
        var old = subscription.GraceUntil;
        subscription.GraceUntil = (old > now ? old.Value : now).AddDays(request.Days);
        subscription.AddEvent(SubscriptionEventType.GraceGranted, now, old, request.Reason, actorId);
        return await SaveAsync(subscription);
    }

    #endregion
}
