using System.ComponentModel.DataAnnotations;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

/// <summary>
/// A user's push setting for one notification type. No row means push is on.
/// </summary>
public class NotificationPreference
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public NotificationType Type { get; set; }

    public bool PushEnabled { get; set; } = true;
}
