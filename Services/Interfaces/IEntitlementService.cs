using HotelManagement.Models.Constants;
using HotelManagement.Models.Entities;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// How much of their plan an owner is using
/// </summary>
public record PlanUsage(int Hotels, int Rooms, int Staff);

/// <summary>
/// Features that only some plans include
/// </summary>
public enum PlanFeature
{
    Inventory,
    FullReports
}

/// <summary>
/// What a hotel owner's plan allows, and the checks run before adding hotels, rooms or staff.
/// Checks throw PlanLimitException (402). SuperAdmins, and hotels a SuperAdmin owns, have no limits.
/// </summary>
public interface IEntitlementService
{
    /// <summary>
    /// The owner's subscription, created on first use: a trial of the top plan, or Free
    /// </summary>
    Task<Subscription> EnsureSubscriptionAsync(string ownerId, bool startTrial = true);

    Task<PlanDefinition> GetPlanForOwnerAsync(string ownerId);

    Task<PlanUsage> GetUsageAsync(string ownerId);

    Task EnsureCanAddHotelAsync(string ownerId);

    Task EnsureCanAddRoomAsync(int hotelId);

    Task EnsureCanAddStaffAsync(int hotelId);

    Task EnsureFeatureAsync(int hotelId, PlanFeature feature);

    /// <summary>
    /// The earliest date reports may cover for these hotels, or null when there's no limit
    /// </summary>
    Task<DateTime?> GetReportHistoryStartAsync(IReadOnlyCollection<int> hotelIds);
}
