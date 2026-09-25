using System.Text.Json.Serialization;

namespace HotelManagement.Models.Enums;

/// <summary>
/// How a subscription is paid for
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingSource
{
    /// <summary>Nothing to charge: the Free plan, a trial, or access granted by a SuperAdmin</summary>
    None = 0,

    /// <summary>The fake payment provider used until a real one is chosen</summary>
    Fake = 1,

    /// <summary>Paid outside the app (e.g. bank transfer) and recorded by a SuperAdmin</summary>
    Manual = 2
}
