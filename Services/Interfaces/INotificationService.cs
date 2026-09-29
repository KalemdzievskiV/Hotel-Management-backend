using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// In-app notifications plus the pushes that go with them. The event methods never throw:
/// a failed notification must not undo or fail the action that caused it.
/// The user who caused an event is never notified about it.
/// </summary>
public interface INotificationService
{
    /// <summary>A guest booked online; tells the hotel's owner and managers</summary>
    Task BookingRequestedAsync(ReservationDto reservation);

    /// <summary>Tells the guest (when they have an account) that the hotel confirmed</summary>
    Task BookingConfirmedAsync(ReservationDto reservation);

    /// <summary>A guest's own cancellation goes to the hotel; the hotel's goes to the guest</summary>
    Task BookingCancelledAsync(ReservationDto reservation, bool byGuest);

    /// <summary>Tells the assignee about a task that is new to them</summary>
    Task TaskAssignedAsync(HousekeepingTaskDto task);

    /// <summary>Tells the assignee that a task they already had became urgent</summary>
    Task TaskBecameUrgentAsync(HousekeepingTaskDto task);

    Task<NotificationPageDto> GetPageAsync(string userId, int page, int pageSize);
    Task<int> GetUnreadCountAsync(string userId);

    /// <summary>False when the notification isn't the user's</summary>
    Task<bool> MarkReadAsync(string userId, int id);
    Task MarkAllReadAsync(string userId);

    /// <summary>Links a phone's push token to the user (moving it from whoever had it)</summary>
    Task RegisterDeviceAsync(string userId, RegisterDeviceDto dto);

    /// <summary>Stops pushes to a phone, e.g. on sign-out; another user's token is left alone</summary>
    Task UnregisterDeviceAsync(string userId, string token);

    /// <summary>The push settings for the types that apply to the user's roles</summary>
    Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(string userId, IList<string> roles);

    Task<IReadOnlyList<NotificationPreferenceDto>> UpdatePreferencesAsync(
        string userId, IList<string> roles, IEnumerable<NotificationPreferenceDto> changes);
}
