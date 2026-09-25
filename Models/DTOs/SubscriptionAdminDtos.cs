using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// One hotel owner's subscription in the SuperAdmin list
/// </summary>
public class SubscriptionSummaryDto
{
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public bool OwnerActive { get; set; }
    public int Hotels { get; set; }
    public int Rooms { get; set; }
    public SubscriptionPlan CurrentPlan { get; set; }
    public SubscriptionPlan SubscribedPlan { get; set; }
    public SubscriptionStatus Status { get; set; }
    public BillingSource Source { get; set; }
    public BillingInterval Interval { get; set; }
    public DateTime? AccessUntil { get; set; }
    public DateTime? GraceUntil { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public int? DaysLeft { get; set; }

    /// <summary>What this owner pays per month (yearly plans spread over 12 months)</summary>
    public decimal MonthlyRevenue { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// A subscription with its usage and full history, for the SuperAdmin detail panel
/// </summary>
public class SubscriptionDetailDto
{
    public SubscriptionSummaryDto Summary { get; set; } = new();
    public BillingOverviewDto Billing { get; set; } = new();
}

public class SubscriptionStatsDto
{
    public int Owners { get; set; }
    public int Trialing { get; set; }
    public int TrialsEndingSoon { get; set; }
    public int Paying { get; set; }
    public int PastDue { get; set; }
    public int Free { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public string Currency { get; set; } = string.Empty;
}

/// <summary>
/// Which subscriptions to list
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionFilter
{
    All,
    Trialing,
    /// <summary>Trials ending within 7 days</summary>
    TrialEndingSoon,
    Paying,
    PastDue,
    /// <summary>Paid by bank transfer</summary>
    Manual,
    Free
}

public abstract class SubscriptionAdminRequest
{
    /// <summary>Why the change was made; shown in the subscription's history</summary>
    [Required(ErrorMessage = "A reason is required")]
    [MinLength(3, ErrorMessage = "A reason is required")]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Extend a trial, a paid period or free access, by a number of days or until a date
/// </summary>
public class ExtendSubscriptionRequest : SubscriptionAdminRequest
{
    [Range(1, 366)]
    public int? Days { get; set; }

    public DateTime? Until { get; set; }
}

/// <summary>
/// A payment made outside the app (e.g. bank transfer), covering the plan until a date
/// </summary>
public class ManualPaymentRequest : SubscriptionAdminRequest
{
    [Required]
    public SubscriptionPlan Plan { get; set; }

    [Required]
    public DateTime Until { get; set; }

    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }
}

/// <summary>
/// Free access to a plan until a date (partners, early customers, goodwill)
/// </summary>
public class GrantAccessRequest : SubscriptionAdminRequest
{
    [Required]
    public SubscriptionPlan Plan { get; set; }

    [Required]
    public DateTime Until { get; set; }
}

public class AdminChangePlanRequest : SubscriptionAdminRequest
{
    [Required]
    public SubscriptionPlan Plan { get; set; }
}

/// <summary>
/// More time for an owner whose payment failed
/// </summary>
public class GrantGraceRequest : SubscriptionAdminRequest
{
    [Range(1, 90)]
    public int Days { get; set; }
}
