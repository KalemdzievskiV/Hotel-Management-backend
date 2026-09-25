using System.Security.Claims;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Models.Constants;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

public class EntitlementService : IEntitlementService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _time;

    public EntitlementService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, TimeProvider time)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>
    /// SuperAdmins support customers, so their own actions aren't limited by the customer's plan
    /// </summary>
    private bool CallerIsSuperAdmin =>
        _httpContextAccessor.HttpContext?.User.IsInRole(AppRoles.SuperAdmin) ?? false;

    private Task<bool> IsSuperAdminAsync(string userId) =>
        (from userRole in _context.UserRoles
         join role in _context.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == AppRoles.SuperAdmin
         select userRole).AnyAsync();

    public async Task<Subscription> EnsureSubscriptionAsync(string ownerId, bool startTrial = true)
    {
        var existing = await _context.Subscriptions.FirstOrDefaultAsync(s => s.OwnerId == ownerId);
        if (existing != null)
            return existing;

        var now = Now;
        var subscription = startTrial
            ? new Subscription
            {
                OwnerId = ownerId,
                Plan = PlanCatalog.TrialPlan,
                Status = SubscriptionStatus.Trialing,
                AccessUntil = now.AddDays(PlanCatalog.TrialDays),
                TrialEndsAt = now.AddDays(PlanCatalog.TrialDays),
                CreatedAt = now
            }
            : new Subscription { OwnerId = ownerId, CreatedAt = now };

        subscription.Events.Add(new SubscriptionEvent
        {
            Type = startTrial ? SubscriptionEventType.TrialStarted : SubscriptionEventType.Downgraded,
            Plan = subscription.Plan,
            NewAccessUntil = subscription.AccessUntil,
            Reason = startTrial ? $"{PlanCatalog.TrialDays}-day trial" : "Started on the Free plan",
            CreatedAt = now
        });

        _context.Subscriptions.Add(subscription);
        try
        {
            await _context.SaveChangesAsync();
            return subscription;
        }
        catch (DbUpdateException)
        {
            // Another request created it first (one per owner is enforced by a unique index)
            _context.Entry(subscription).State = EntityState.Detached;
            return await _context.Subscriptions.FirstAsync(s => s.OwnerId == ownerId);
        }
    }

    public async Task<PlanDefinition> GetPlanForOwnerAsync(string ownerId)
    {
        var subscription = await EnsureSubscriptionAsync(ownerId);
        return PlanCatalog.Get(subscription.GetEffectivePlan(Now));
    }

    public async Task<PlanUsage> GetUsageAsync(string ownerId)
    {
        var hotelIds = await _context.Hotels.Where(h => h.OwnerId == ownerId).Select(h => h.Id).ToListAsync();
        var rooms = await _context.Rooms.CountAsync(r => hotelIds.Contains(r.HotelId));
        return new PlanUsage(hotelIds.Count, rooms, await CountStaffAsync(hotelIds));
    }

    /// <summary>
    /// Active managers and housekeepers working at the given hotels
    /// </summary>
    private Task<int> CountStaffAsync(IReadOnlyCollection<int> hotelIds) =>
        (from user in _context.Users
         where user.IsActive && user.HotelId != null && hotelIds.Contains(user.HotelId.Value)
         where (from userRole in _context.UserRoles
                join role in _context.Roles on userRole.RoleId equals role.Id
                where userRole.UserId == user.Id && (role.Name == AppRoles.Manager || role.Name == AppRoles.Housekeeper)
                select userRole).Any()
         select user).CountAsync();

    /// <summary>
    /// The owner whose plan applies, or null when no limits apply
    /// </summary>
    private async Task<string?> GetLimitedOwnerAsync(string ownerId)
    {
        if (CallerIsSuperAdmin || await IsSuperAdminAsync(ownerId))
            return null;
        return ownerId;
    }

    private async Task<string?> GetLimitedOwnerOfHotelAsync(int hotelId)
    {
        var ownerId = await _context.Hotels.Where(h => h.Id == hotelId).Select(h => h.OwnerId).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Hotel with ID {hotelId} not found");
        return await GetLimitedOwnerAsync(ownerId);
    }

    public async Task EnsureCanAddHotelAsync(string ownerId)
    {
        if (await GetLimitedOwnerAsync(ownerId) == null)
            return;

        var plan = await GetPlanForOwnerAsync(ownerId);
        var usage = await GetUsageAsync(ownerId);
        EnsureWithin(plan, "hotels", usage.Hotels, plan.MaxHotels, p => p.MaxHotels);
    }

    public async Task EnsureCanAddRoomAsync(int hotelId)
    {
        var ownerId = await GetLimitedOwnerOfHotelAsync(hotelId);
        if (ownerId == null)
            return;

        var plan = await GetPlanForOwnerAsync(ownerId);
        var usage = await GetUsageAsync(ownerId);
        EnsureWithin(plan, "rooms", usage.Rooms, plan.MaxRooms, p => p.MaxRooms);
    }

    public async Task EnsureCanAddStaffAsync(int hotelId)
    {
        var ownerId = await GetLimitedOwnerOfHotelAsync(hotelId);
        if (ownerId == null)
            return;

        var plan = await GetPlanForOwnerAsync(ownerId);
        var usage = await GetUsageAsync(ownerId);
        EnsureWithin(plan, "staff", usage.Staff, plan.MaxStaff, p => p.MaxStaff);
    }

    private static void EnsureWithin(PlanDefinition plan, string limit, int used, int? allowed, Func<PlanDefinition, int?> limitOf)
    {
        if (allowed == null || used < allowed)
            return;

        var upgrade = PlanCatalog.All.FirstOrDefault(p => p.Plan > plan.Plan && (limitOf(p) == null || limitOf(p) > used));
        var suggestion = upgrade == null ? "" : $" Upgrade to {upgrade.Name} for more.";
        var noun = allowed == 1 && limit.EndsWith('s') ? limit[..^1] : limit; // "1 hotel", "5 rooms", "1 staff"
        throw new PlanLimitException(
            $"The {plan.Name} plan includes up to {allowed} {noun}, and you're using {used}.{suggestion}",
            limit, plan.Plan, allowed, upgrade?.Plan);
    }

    public async Task EnsureFeatureAsync(int hotelId, PlanFeature feature)
    {
        var ownerId = await GetLimitedOwnerOfHotelAsync(hotelId);
        if (ownerId == null)
            return;

        var plan = await GetPlanForOwnerAsync(ownerId);
        if (Includes(plan, feature))
            return;

        var upgrade = PlanCatalog.All.First(p => p.Plan > plan.Plan && Includes(p, feature));
        var name = feature == PlanFeature.Inventory ? "Inventory" : "Full reports";
        throw new PlanLimitException(
            $"{name} isn't included in the {plan.Name} plan. Upgrade to {upgrade.Name} to use it.",
            feature.ToString().ToLowerInvariant(), plan.Plan, null, upgrade.Plan);
    }

    private static bool Includes(PlanDefinition plan, PlanFeature feature) => feature switch
    {
        PlanFeature.Inventory => plan.Inventory,
        PlanFeature.FullReports => plan.FullReports,
        _ => false
    };

    public async Task<DateTime?> GetReportHistoryStartAsync(IReadOnlyCollection<int> hotelIds)
    {
        if (CallerIsSuperAdmin)
            return null;

        var ownerIds = await _context.Hotels.Where(h => hotelIds.Contains(h.Id)).Select(h => h.OwnerId).Distinct().ToListAsync();
        foreach (var ownerId in ownerIds)
        {
            if (await GetLimitedOwnerAsync(ownerId) != null && !(await GetPlanForOwnerAsync(ownerId)).FullReports)
                return Now.Date.AddDays(-PlanCatalog.LimitedReportDays);
        }
        return null;
    }
}
