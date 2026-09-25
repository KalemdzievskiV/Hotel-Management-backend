using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HotelManagement.Models.Constants;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// A plan as shown on the pricing page. Null limits are unlimited.
/// </summary>
public class PlanDto
{
    public SubscriptionPlan Plan { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }
    public string Currency { get; set; } = PlanCatalog.Currency;
    public int? MaxHotels { get; set; }
    public int? MaxRooms { get; set; }
    public int? MaxStaff { get; set; }
    public bool Inventory { get; set; }
    public bool FullReports { get; set; }
    public int? ReportHistoryDays { get; set; }

    public static PlanDto From(PlanDefinition plan) => new()
    {
        Plan = plan.Plan,
        Name = plan.Name,
        MonthlyPrice = plan.MonthlyPrice,
        YearlyPrice = plan.YearlyPrice,
        MaxHotels = plan.MaxHotels,
        MaxRooms = plan.MaxRooms,
        MaxStaff = plan.MaxStaff,
        Inventory = plan.Inventory,
        FullReports = plan.FullReports,
        ReportHistoryDays = plan.FullReports ? null : PlanCatalog.LimitedReportDays
    };
}

public class PlanUsageDto
{
    public int Hotels { get; set; }
    public int Rooms { get; set; }
    public int Staff { get; set; }
}

public class SubscriptionEventDto
{
    public SubscriptionEventType Type { get; set; }
    public SubscriptionPlan Plan { get; set; }
    public DateTime? OldAccessUntil { get; set; }
    public DateTime? NewAccessUntil { get; set; }
    public decimal? Amount { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    /// <summary>Who made the change; null for payments and automatic changes</summary>
    public string? ActorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// An owner's subscription as shown on their Billing page
/// </summary>
public class BillingOverviewDto
{
    /// <summary>The plan in effect right now (Free once a trial or payment has lapsed)</summary>
    public SubscriptionPlan CurrentPlan { get; set; }

    /// <summary>The plan being trialled or paid for</summary>
    public SubscriptionPlan SubscribedPlan { get; set; }

    public SubscriptionStatus Status { get; set; }
    public BillingInterval Interval { get; set; }
    public BillingSource Source { get; set; }
    public DateTime? AccessUntil { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? GraceUntil { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public SubscriptionPlan? ScheduledPlan { get; set; }

    /// <summary>Days until the trial, paid period or grace period ends</summary>
    public int? DaysLeft { get; set; }

    /// <summary>Whether a new checkout makes sense (not already paying automatically)</summary>
    public bool CanCheckout { get; set; }

    public PlanDto Limits { get; set; } = new();
    public PlanUsageDto Usage { get; set; } = new();
    public List<PlanDto> Plans { get; set; } = new();
    public List<SubscriptionEventDto> History { get; set; } = new();
}

public class CheckoutRequest
{
    [Required]
    public SubscriptionPlan Plan { get; set; }

    public BillingInterval Interval { get; set; } = BillingInterval.Monthly;
}

public class CheckoutResponse
{
    /// <summary>Where to send the owner to pay; relative for the app's own test checkout</summary>
    public string CheckoutUrl { get; set; } = string.Empty;
}

public class ChangePlanRequest
{
    [Required]
    public SubscriptionPlan Plan { get; set; }
}

/// <summary>
/// What the fake checkout page shows
/// </summary>
public class FakeCheckoutDto
{
    public SubscriptionPlan Plan { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public BillingInterval Interval { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class CompleteFakeCheckoutRequest
{
    /// <summary>false simulates a declined card</summary>
    public bool Approve { get; set; } = true;
}

/// <summary>
/// Trying out billing without waiting for renewal dates
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SimulateBillingAction
{
    RenewalPaid,
    RenewalFailed,
    RunMaintenance
}

public class SimulateBillingRequest
{
    public string? OwnerId { get; set; }
    public SimulateBillingAction Action { get; set; }
}
