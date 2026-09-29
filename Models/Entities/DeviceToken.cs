using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.Entities;

/// <summary>
/// An Expo push token of a phone the user is signed in on. A token belongs to one user at a
/// time: signing in as someone else on the same phone moves it.
/// </summary>
public class DeviceToken
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    /// <summary>ExponentPushToken[...]</summary>
    [MaxLength(200)]
    public string Token { get; set; } = string.Empty;

    /// <summary>"android" or "ios"</summary>
    [MaxLength(20)]
    public string Platform { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Last time the app registered it; the app does that on every start</summary>
    public DateTime LastSeenAt { get; set; }
}
