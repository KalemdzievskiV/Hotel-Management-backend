using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.Entities;

/// <summary>
/// A browser the user turned on push notifications in (the web app, including when it's
/// added to a phone's home screen). Like a device token, it belongs to one user at a time.
/// </summary>
public class WebPushSubscription
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    /// <summary>The push service URL that identifies this browser</summary>
    [MaxLength(1000)]
    public string Endpoint { get; set; } = string.Empty;

    [MaxLength(200)]
    public string P256dh { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Auth { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime LastSeenAt { get; set; }
}
