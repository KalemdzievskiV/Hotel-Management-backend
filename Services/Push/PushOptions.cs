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

    /// <summary>
    /// VAPID keys for browser push (base64url). Web push is off until both are set. Never put
    /// them in appsettings: locally use `dotnet user-secrets set Push:VapidPublicKey ...` (and
    /// Push:VapidPrivateKey); in production the Push__VapidPublicKey / Push__VapidPrivateKey
    /// environment variables.
    /// </summary>
    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }

    /// <summary>Contact for push services, a mailto: or https: URL</summary>
    public string VapidSubject { get; set; } = "mailto:admin@hotel.com";

    public bool WebPushEnabled => Enabled && !string.IsNullOrEmpty(VapidPublicKey) && !string.IsNullOrEmpty(VapidPrivateKey);
}
