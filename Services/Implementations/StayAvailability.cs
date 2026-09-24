using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;

namespace HotelManagement.Services.Implementations;

/// <summary>
/// Decides whether a stay fits in a room, as a pure function of the existing stays.
///
/// Every stay occupies a time window. Short stays use their exact times. Overnight stays
/// entered as dates only (midnight) use the hotel's standard check-in/check-out times,
/// so a guest leaving at 11:00 and the next arriving at 14:00 on the same day is fine.
/// After an overnight stay the room needs the hotel's cleaning buffer before the next
/// stay starts; short stays need no buffer.
/// </summary>
public static class StayAvailability
{
    private static readonly TimeSpan DefaultCheckInTime = new(14, 0, 0);
    private static readonly TimeSpan DefaultCheckOutTime = new(11, 0, 0);

    public readonly record struct StayWindow(DateTime Start, DateTime End);

    public static StayWindow GetWindow(DateTime checkIn, DateTime checkOut, BookingType bookingType, Hotel? hotel)
    {
        if (bookingType == BookingType.ShortStay)
            return new StayWindow(checkIn, checkOut);

        var start = checkIn.TimeOfDay == TimeSpan.Zero
            ? checkIn.Date + ParseTime(hotel?.CheckInTime, DefaultCheckInTime)
            : checkIn;
        var end = checkOut.TimeOfDay == TimeSpan.Zero
            ? checkOut.Date + ParseTime(hotel?.CheckOutTime, DefaultCheckOutTime)
            : checkOut;

        return new StayWindow(start, end);
    }

    /// <summary>
    /// Returns a description of the first conflicting reservation, or null if the stay fits.
    /// Existing reservations should already exclude cancelled/checked-out/no-show ones.
    /// </summary>
    public static string? FindConflict(
        DateTime checkIn,
        DateTime checkOut,
        BookingType bookingType,
        Hotel? hotel,
        IEnumerable<Reservation> existingReservations)
    {
        var bufferHours = hotel?.BufferTimeHours ?? 0;
        var requested = GetWindow(checkIn, checkOut, bookingType, hotel);
        var requestedBuffer = BufferAfter(bookingType, bufferHours);

        foreach (var existing in existingReservations.OrderBy(r => r.CheckInDate))
        {
            var window = GetWindow(existing.CheckInDate, existing.CheckOutDate, existing.BookingType, hotel);
            var existingBuffer = BufferAfter(existing.BookingType, bufferHours);

            var overlaps = requested.Start < window.End + existingBuffer
                        && window.Start < requested.End + requestedBuffer;
            if (!overlaps)
                continue;

            var guestName = existing.Guest != null ? $"{existing.Guest.FirstName} {existing.Guest.LastName}" : "Unknown";
            return $"{DescribeConflict(requested, bookingType, existing, window, existingBuffer)}. Guest: {guestName}, Reservation #{existing.Id}";
        }

        return null;
    }

    private static TimeSpan BufferAfter(BookingType bookingType, int bufferHours) =>
        bookingType == BookingType.Daily ? TimeSpan.FromHours(bufferHours) : TimeSpan.Zero;

    private static string DescribeConflict(
        StayWindow requested,
        BookingType requestedType,
        Reservation existing,
        StayWindow existingWindow,
        TimeSpan existingBuffer)
    {
        if (existing.BookingType == BookingType.ShortStay)
            return $"Overlaps with short-stay booking from {existingWindow.Start:g} to {existingWindow.End:g}";

        if (requestedType == BookingType.ShortStay)
            return $"Room is booked overnight from {existingWindow.Start:d} to {existingWindow.End:d}";

        // Starts after the previous guest leaves, but inside the cleaning buffer
        if (requested.Start >= existingWindow.End)
        {
            var earliest = existingWindow.End + existingBuffer;
            return $"Room is occupied until {existingWindow.End:g}. Earliest check-in: {earliest:g} ({existingBuffer.TotalHours:0}h cleaning buffer)";
        }

        return $"Overlaps with existing reservation from {existingWindow.Start:d} to {existingWindow.End:d}";
    }

    private static TimeSpan ParseTime(string? value, TimeSpan fallback) =>
        TimeSpan.TryParse(value, out var time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1) ? time : fallback;
}
