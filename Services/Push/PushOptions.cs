namespace HotelManagement.Services.Push;

public class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>Turns sending off (notifications are still saved for the in-app list)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Only needed when "Enhanced push security" is on for the Expo project
    /// (Push__ExpoAccessToken environment variable; never in appsettings.json)
    /// </summary>
    public string? ExpoAccessToken { get; set; }
}
