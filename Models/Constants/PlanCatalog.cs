using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Constants;

/// <summary>
/// What each plan costs and allows. A null limit means unlimited.
/// </summary>
public record PlanDefinition(
    SubscriptionPlan Plan,
    string Name,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int? MaxHotels,
    int? MaxRooms,
    int? MaxStaff,
    bool Inventory,
    bool FullReports)
{
    public decimal PriceFor(BillingInterval interval) =>
        interval == BillingInterval.Yearly ? YearlyPrice : MonthlyPrice;
}

public static class PlanCatalog
{
    public const string Currency = "EUR";

    /// <summary>New owners try the top plan for this long, then drop to Free unless they pay</summary>
    public const int TrialDays = 30;

    /// <summary>How long a plan keeps working after a renewal payment fails</summary>
    public const int GraceDays = 14;

    /// <summary>How far back reports reach on plans without full reports</summary>
    public const int LimitedReportDays = 30;

    /// <summary>The most a SuperAdmin can extend or grant in one action</summary>
    public const int MaxAdminExtensionDays = 366;

    public const SubscriptionPlan TrialPlan = SubscriptionPlan.Pro;

    public static readonly PlanDefinition Free = new(
        SubscriptionPlan.Free, "Free", 0m, 0m,
        MaxHotels: 1, MaxRooms: 5, MaxStaff: 1, Inventory: false, FullReports: false);

    public static readonly PlanDefinition Starter = new(
        SubscriptionPlan.Starter, "Starter", 12m, 120m,
        MaxHotels: 1, MaxRooms: 20, MaxStaff: 5, Inventory: true, FullReports: true);

    public static readonly PlanDefinition Pro = new(
        SubscriptionPlan.Pro, "Pro", 29m, 290m,
        MaxHotels: 3, MaxRooms: 60, MaxStaff: null, Inventory: true, FullReports: true);

    public static IReadOnlyList<PlanDefinition> All { get; } = new[] { Free, Starter, Pro };

    public static PlanDefinition Get(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Starter => Starter,
        SubscriptionPlan.Pro => Pro,
        _ => Free
    };
}
