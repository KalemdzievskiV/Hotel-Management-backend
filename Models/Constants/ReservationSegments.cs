namespace HotelManagement.Models.Constants;

/// <summary>The front desk's views of a hotel's bookings (GET /Reservations/search)</summary>
public static class ReservationSegments
{
    public const string Arrivals = "arrivals";
    public const string Departures = "departures";
    public const string InHouse = "inhouse";
    public const string Upcoming = "upcoming";
    public const string Pending = "pending";
    public const string All = "all";

    public static readonly string[] Values = { Arrivals, Departures, InHouse, Upcoming, Pending, All };
}
