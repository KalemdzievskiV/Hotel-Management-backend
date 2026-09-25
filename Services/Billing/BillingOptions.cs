namespace HotelManagement.Services.Billing;

/// <summary>
/// "Billing" configuration section
/// </summary>
public class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Signs fake checkout links and webhooks. A random key is used when not set.</summary>
    public string? FakeSigningKey { get; set; }

    /// <summary>Where the app's own fake checkout page lives (in the frontend)</summary>
    public string FakeCheckoutPath { get; set; } = "/dashboard/billing/checkout";

    /// <summary>Whether the background job that renews and expires subscriptions runs</summary>
    public bool RunMaintenance { get; set; } = true;

    public int MaintenanceIntervalMinutes { get; set; } = 60;
}
