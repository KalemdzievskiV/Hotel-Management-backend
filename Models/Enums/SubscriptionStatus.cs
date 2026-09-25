using System.Text.Json.Serialization;

namespace HotelManagement.Models.Enums;

/// <summary>
/// Where a subscription is in its life. Whether the owner currently gets their plan
/// is decided by Subscription.GetEffectivePlan, which also looks at the dates.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionStatus
{
    /// <summary>Free plan, or a paid plan that is paid up</summary>
    Active = 0,

    /// <summary>Trying a paid plan until AccessUntil, without paying</summary>
    Trialing = 1,

    /// <summary>A renewal payment failed; the plan continues until GraceUntil</summary>
    PastDue = 2,

    /// <summary>A trial or paid plan ran out and the owner is back on Free</summary>
    Expired = 3
}
