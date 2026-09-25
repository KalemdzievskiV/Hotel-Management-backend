using System.Text.Json.Serialization;

namespace HotelManagement.Models.Enums;

/// <summary>
/// Entries in a subscription's history
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionEventType
{
    TrialStarted = 1,
    TrialExtended = 2,
    Subscribed = 3,
    Renewed = 4,
    PaymentFailed = 5,
    GraceGranted = 6,
    Expired = 7,
    CancelScheduled = 8,
    CancelReverted = 9,
    PlanChanged = 10,
    Extended = 11,
    ManualPayment = 12,
    AccessGranted = 13,
    Downgraded = 14
}
