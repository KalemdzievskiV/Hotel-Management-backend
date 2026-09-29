using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public string TypeName => Type.ToString();
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int? HotelId { get; set; }
    public int? ReservationId { get; set; }
    public int? HousekeepingTaskId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead => ReadAt.HasValue;
}

public class NotificationPageDto : PagedResult<NotificationDto>
{
    public int UnreadCount { get; set; }
}

public class RegisterDeviceDto
{
    /// <summary>The Expo push token, ExponentPushToken[...]</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>"android" or "ios"</summary>
    public string Platform { get; set; } = string.Empty;
}

public class UnregisterDeviceDto
{
    public string Token { get; set; } = string.Empty;
}

public class NotificationPreferenceDto
{
    public NotificationType Type { get; set; }
    public string TypeName => Type.ToString();
    public bool PushEnabled { get; set; }
}
