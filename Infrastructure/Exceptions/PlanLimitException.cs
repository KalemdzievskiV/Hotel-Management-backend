using HotelManagement.Models.Enums;

namespace HotelManagement.Infrastructure.Exceptions;

/// <summary>
/// The owner's plan doesn't allow this (too many rooms, a feature not in the plan, ...).
/// Returned as 402 with the details in data, so the app can offer an upgrade.
/// </summary>
public class PlanLimitException : Exception
{
    /// <summary>What ran out: "hotels", "rooms", "staff" or a feature such as "inventory"</summary>
    public string Limit { get; }

    public SubscriptionPlan CurrentPlan { get; }

    /// <summary>The allowance on the current plan; null for features the plan doesn't include</summary>
    public int? Allowed { get; }

    /// <summary>The cheapest plan that allows it, if any</summary>
    public SubscriptionPlan? UpgradeTo { get; }

    public PlanLimitException(string message, string limit, SubscriptionPlan currentPlan, int? allowed, SubscriptionPlan? upgradeTo)
        : base(message)
    {
        Limit = limit;
        CurrentPlan = currentPlan;
        Allowed = allowed;
        UpgradeTo = upgradeTo;
    }
}
