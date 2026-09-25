using System.Text.Json.Serialization;

namespace HotelManagement.Models.Enums;

/// <summary>
/// What a hotel owner's subscription allows; limits and prices live in PlanCatalog
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionPlan
{
    Free = 0,
    Starter = 1,
    Pro = 2
}
