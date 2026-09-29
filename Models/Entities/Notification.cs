using System.ComponentModel.DataAnnotations;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

/// <summary>
/// One entry in a user's in-app notification list. A push is sent for it too, unless the user
/// turned that type off. The ids point at what it's about; they aren't foreign keys, so a
/// deleted booking or task leaves the notification in place.
/// </summary>
public class Notification
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public NotificationType Type { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Body { get; set; } = string.Empty;

    public int? HotelId { get; set; }

    public int? ReservationId { get; set; }

    public int? HousekeepingTaskId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }
}
