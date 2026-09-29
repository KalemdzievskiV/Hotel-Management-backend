namespace HotelManagement.Models.Enums;

/// <summary>
/// What a notification is about. Each type can have its push turned off separately;
/// the in-app list always keeps every notification.
/// </summary>
public enum NotificationType
{
    /// <summary>A guest booked online and the booking waits for approval (to the hotel's managers)</summary>
    NewBooking = 1,

    /// <summary>A guest cancelled their own booking (to the hotel's managers)</summary>
    BookingCancelledByGuest = 2,

    /// <summary>The hotel confirmed the guest's booking (to the guest)</summary>
    BookingConfirmed = 3,

    /// <summary>The hotel cancelled the guest's booking (to the guest)</summary>
    BookingCancelled = 4,

    /// <summary>A housekeeping task was assigned to the user</summary>
    TaskAssigned = 5,

    /// <summary>A task already assigned to the user became urgent</summary>
    TaskUrgent = 6
}
