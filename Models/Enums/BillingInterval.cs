using System.Text.Json.Serialization;

namespace HotelManagement.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingInterval
{
    Monthly = 0,
    Yearly = 1
}
