using System.Globalization;
using System.Security.Claims;
using HotelManagement.Data;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using HotelManagement.Services.Push;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

public class NotificationService : INotificationService
{
    private static readonly NotificationType[] ManagerTypes =
    [
        NotificationType.NewBooking,
        NotificationType.BookingCancelledByGuest,
        NotificationType.TaskAssigned,
        NotificationType.TaskUrgent
    ];

    private static readonly NotificationType[] HousekeeperTypes = [NotificationType.TaskAssigned, NotificationType.TaskUrgent];

    private static readonly NotificationType[] GuestTypes = [NotificationType.BookingConfirmed, NotificationType.BookingCancelled];

    private readonly ApplicationDbContext _context;
    private readonly IPushQueue _push;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _time;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        IPushQueue push,
        IHttpContextAccessor httpContextAccessor,
        TimeProvider time,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _push = push;
        _httpContextAccessor = httpContextAccessor;
        _time = time;
        _logger = logger;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private string? ActorId => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    #region Events

    public Task BookingRequestedAsync(ReservationDto r) =>
        SafelyAsync(nameof(BookingRequestedAsync), async () =>
            await NotifyAsync(await HotelManagerIdsAsync(r.HotelId), new Notification
            {
                Type = NotificationType.NewBooking,
                Title = "New booking to approve",
                Body = $"{r.GuestName} · Room {r.RoomNumber} · {Stay(r)}",
                HotelId = r.HotelId,
                ReservationId = r.Id
            }));

    public Task BookingConfirmedAsync(ReservationDto r) =>
        SafelyAsync(nameof(BookingConfirmedAsync), async () =>
            await NotifyAsync(await GuestUserIdsAsync(r.GuestId), new Notification
            {
                Type = NotificationType.BookingConfirmed,
                Title = "Your booking is confirmed",
                Body = $"{r.HotelName} · {Stay(r)}",
                HotelId = r.HotelId,
                ReservationId = r.Id
            }));

    public Task BookingCancelledAsync(ReservationDto r, bool byGuest) =>
        SafelyAsync(nameof(BookingCancelledAsync), async () =>
        {
            if (byGuest)
            {
                await NotifyAsync(await HotelManagerIdsAsync(r.HotelId), new Notification
                {
                    Type = NotificationType.BookingCancelledByGuest,
                    Title = "Booking cancelled by the guest",
                    Body = $"{r.GuestName} · Room {r.RoomNumber} · {Stay(r)}",
                    HotelId = r.HotelId,
                    ReservationId = r.Id
                });
                return;
            }

            var reason = string.IsNullOrWhiteSpace(r.CancellationReason) ? "" : $". Reason: {r.CancellationReason}";
            await NotifyAsync(await GuestUserIdsAsync(r.GuestId), new Notification
            {
                Type = NotificationType.BookingCancelled,
                Title = "Your booking was cancelled",
                Body = $"{r.HotelName} · {Stay(r)}{reason}",
                HotelId = r.HotelId,
                ReservationId = r.Id
            });
        });

    public Task TaskAssignedAsync(HousekeepingTaskDto t) =>
        SafelyAsync(nameof(TaskAssignedAsync), async () =>
        {
            if (string.IsNullOrEmpty(t.AssignedToUserId))
                return;

            var urgent = t.Priority == HousekeepingTaskPriority.Urgent;
            await NotifyAsync([t.AssignedToUserId], new Notification
            {
                Type = NotificationType.TaskAssigned,
                Title = urgent ? $"Urgent task: Room {t.RoomNumber}" : $"New task: Room {t.RoomNumber}",
                Body = urgent ? TaskLabel(t.Type) : $"{TaskLabel(t.Type)} · {t.Priority} priority",
                HotelId = t.HotelId,
                HousekeepingTaskId = t.Id
            });
        });

    public Task TaskBecameUrgentAsync(HousekeepingTaskDto t) =>
        SafelyAsync(nameof(TaskBecameUrgentAsync), async () =>
        {
            if (string.IsNullOrEmpty(t.AssignedToUserId))
                return;

            await NotifyAsync([t.AssignedToUserId], new Notification
            {
                Type = NotificationType.TaskUrgent,
                Title = $"Now urgent: Room {t.RoomNumber}",
                Body = TaskLabel(t.Type),
                HotelId = t.HotelId,
                HousekeepingTaskId = t.Id
            });
        });

    private async Task SafelyAsync(string name, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification {Name} failed", name);
        }
    }

    /// <summary>
    /// Saves one copy of the notification per recipient, then queues a push to each of their
    /// phones unless they turned this type off
    /// </summary>
    private async Task NotifyAsync(IEnumerable<string> userIds, Notification template)
    {
        var actorId = ActorId;
        var recipients = userIds.Where(id => id != actorId).Distinct().ToList();
        if (recipients.Count == 0)
            return;

        var notifications = recipients.Select(userId => new Notification
        {
            UserId = userId,
            Type = template.Type,
            Title = Truncate(template.Title, 200),
            Body = Truncate(template.Body, 500),
            HotelId = template.HotelId,
            ReservationId = template.ReservationId,
            HousekeepingTaskId = template.HousekeepingTaskId,
            CreatedAt = Now
        }).ToList();
        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        var muted = await _context.NotificationPreferences
            .Where(p => recipients.Contains(p.UserId) && p.Type == template.Type && !p.PushEnabled)
            .Select(p => p.UserId)
            .ToListAsync();
        var devices = await _context.DeviceTokens
            .Where(d => recipients.Contains(d.UserId) && !muted.Contains(d.UserId))
            .Select(d => new { d.UserId, d.Token })
            .ToListAsync();

        foreach (var device in devices)
        {
            var notification = notifications.First(n => n.UserId == device.UserId);
            _push.Enqueue(new PushMessage(device.Token, notification.Title, notification.Body, PushData(notification)));
        }
    }

    /// <summary>What the app needs to open the right screen when the push is tapped</summary>
    private static Dictionary<string, object?> PushData(Notification n)
    {
        var data = new Dictionary<string, object?>
        {
            ["notificationId"] = n.Id,
            ["type"] = n.Type.ToString()
        };
        if (n.HotelId.HasValue) data["hotelId"] = n.HotelId;
        if (n.ReservationId.HasValue) data["reservationId"] = n.ReservationId;
        if (n.HousekeepingTaskId.HasValue) data["taskId"] = n.HousekeepingTaskId;
        return data;
    }

    /// <summary>The hotel's owner and its active Admins and Managers</summary>
    private async Task<List<string>> HotelManagerIdsAsync(int hotelId)
    {
        var managerRoleIds = _context.Roles
            .Where(r => r.Name == AppRoles.Admin || r.Name == AppRoles.Manager)
            .Select(r => r.Id);
        var managerIds = _context.UserRoles
            .Where(ur => managerRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId);
        var ownerId = _context.Hotels.Where(h => h.Id == hotelId).Select(h => h.OwnerId);

        return await _context.Users
            .Where(u => u.IsActive && (ownerId.Contains(u.Id) || (u.HotelId == hotelId && managerIds.Contains(u.Id))))
            .Select(u => u.Id)
            .ToListAsync();
    }

    /// <summary>The guest's own account, if they have one (walk-ins and phone bookings don't)</summary>
    private async Task<List<string>> GuestUserIdsAsync(int guestId)
    {
        var userId = await _context.Guests.Where(g => g.Id == guestId).Select(g => g.UserId).FirstOrDefaultAsync();
        return string.IsNullOrEmpty(userId) ? [] : [userId];
    }

    /// <summary>"12 Oct – 14 Oct", or "12 Oct, 14:00–17:00" for a short stay</summary>
    private static string Stay(ReservationDto r)
    {
        var culture = CultureInfo.InvariantCulture;
        return r.BookingType == BookingType.ShortStay
            ? $"{r.CheckInDate.ToString("d MMM, HH:mm", culture)}–{r.CheckOutDate.ToString("HH:mm", culture)}"
            : $"{r.CheckInDate.ToString("d MMM", culture)} – {r.CheckOutDate.ToString("d MMM", culture)}";
    }

    private static string TaskLabel(HousekeepingTaskType type) => type switch
    {
        HousekeepingTaskType.CleanRoom => "Clean room",
        HousekeepingTaskType.ChangeLinen => "Change linen",
        HousekeepingTaskType.DeepClean => "Deep clean",
        HousekeepingTaskType.TurnDown => "Turn-down",
        _ => type.ToString()
    };

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    #endregion

    #region The user's list

    public async Task<NotificationPageDto> GetPageAsync(string userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var mine = _context.Notifications.Where(n => n.UserId == userId);
        var items = await mine
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Body = n.Body,
                HotelId = n.HotelId,
                ReservationId = n.ReservationId,
                HousekeepingTaskId = n.HousekeepingTaskId,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            })
            .ToListAsync();

        return new NotificationPageDto
        {
            Items = items,
            TotalCount = await mine.CountAsync(),
            UnreadCount = await mine.CountAsync(n => n.ReadAt == null),
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<int> GetUnreadCountAsync(string userId) =>
        _context.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null);

    public async Task<bool> MarkReadAsync(string userId, int id)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification == null)
            return false;

        notification.ReadAt ??= Now;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllReadAsync(string userId)
    {
        var unread = await _context.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync();
        foreach (var n in unread)
            n.ReadAt = Now;
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Devices

    public async Task RegisterDeviceAsync(string userId, RegisterDeviceDto dto)
    {
        var device = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.Token == dto.Token);
        if (device == null)
        {
            device = new DeviceToken { Token = dto.Token, CreatedAt = Now };
            _context.DeviceTokens.Add(device);
        }

        device.UserId = userId;
        device.Platform = dto.Platform;
        device.LastSeenAt = Now;
        await _context.SaveChangesAsync();
    }

    public async Task UnregisterDeviceAsync(string userId, string token)
    {
        var device = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token && d.UserId == userId);
        if (device == null)
            return;

        _context.DeviceTokens.Remove(device);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Preferences

    private static List<NotificationType> TypesFor(IList<string> roles)
    {
        var types = new HashSet<NotificationType>();
        if (roles.Contains(AppRoles.SuperAdmin) || roles.Contains(AppRoles.Admin) || roles.Contains(AppRoles.Manager))
            types.UnionWith(ManagerTypes);
        if (roles.Contains(AppRoles.Housekeeper))
            types.UnionWith(HousekeeperTypes);
        if (roles.Contains(AppRoles.Guest))
            types.UnionWith(GuestTypes);
        return types.Order().ToList();
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(string userId, IList<string> roles)
    {
        var off = await _context.NotificationPreferences
            .Where(p => p.UserId == userId && !p.PushEnabled)
            .Select(p => p.Type)
            .ToListAsync();

        return TypesFor(roles)
            .Select(type => new NotificationPreferenceDto { Type = type, PushEnabled = !off.Contains(type) })
            .ToList();
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> UpdatePreferencesAsync(
        string userId, IList<string> roles, IEnumerable<NotificationPreferenceDto> changes)
    {
        var allowed = TypesFor(roles);
        var existing = await _context.NotificationPreferences.Where(p => p.UserId == userId).ToListAsync();

        foreach (var change in changes.Where(c => allowed.Contains(c.Type)))
        {
            var preference = existing.FirstOrDefault(p => p.Type == change.Type);
            if (preference == null)
            {
                preference = new NotificationPreference { UserId = userId, Type = change.Type };
                _context.NotificationPreferences.Add(preference);
                existing.Add(preference);
            }
            preference.PushEnabled = change.PushEnabled;
        }

        await _context.SaveChangesAsync();
        return await GetPreferencesAsync(userId, roles);
    }

    #endregion
}
