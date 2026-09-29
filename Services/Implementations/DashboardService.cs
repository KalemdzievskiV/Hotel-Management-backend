using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

/// <summary>
/// Numbers for the front desk. Booking dates are hotel wall-clock days, so "today" is the day
/// the caller passes (the hotel's own date), not the server's UTC date.
/// </summary>
public class DashboardService : IDashboardService
{
    private const int MaxAttentionItems = 20;

    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TodayDashboardDto> GetTodayAsync(int hotelId, DateTime date, int utcOffsetMinutes)
    {
        var day = date.Date;
        var tomorrow = day.AddDays(1);

        var rooms = await _context.Rooms.Where(r => r.HotelId == hotelId && r.IsActive).ToListAsync();

        // Everything that touches today, or is still waiting on the desk
        var reservations = await _context.Reservations
            .Include(r => r.Guest)
            .Include(r => r.Room)
            .Where(r => r.HotelId == hotelId && (
                r.Status == ReservationStatus.CheckedIn
                || r.Status == ReservationStatus.Pending
                || (r.Status == ReservationStatus.Confirmed && r.CheckInDate < tomorrow)
                || (r.CheckInDate >= day && r.CheckInDate < tomorrow)
                || (r.CheckOutDate >= day && r.CheckOutDate < tomorrow)))
            .ToListAsync();

        bool StartsToday(Reservation r) => r.CheckInDate >= day && r.CheckInDate < tomorrow;
        bool EndsToday(Reservation r) => r.CheckOutDate >= day && r.CheckOutDate < tomorrow;

        var arrivals = reservations
            .Where(r => StartsToday(r) && r.Status is ReservationStatus.Pending or ReservationStatus.Confirmed or ReservationStatus.CheckedIn)
            .ToList();
        var departures = reservations
            .Where(r => EndsToday(r) && r.Status is ReservationStatus.CheckedIn or ReservationStatus.CheckedOut)
            .ToList();
        var inHouse = reservations.Where(r => r.Status == ReservationStatus.CheckedIn).ToList();
        var occupied = inHouse.Select(r => r.RoomId).Distinct().Count();

        var openTasks = await _context.HousekeepingTasks
            .CountAsync(t => t.Room.HotelId == hotelId
                && t.ScheduledFor >= day && t.ScheduledFor < tomorrow
                && (t.Status == HousekeepingTaskStatus.Pending || t.Status == HousekeepingTaskStatus.InProgress));

        return new TodayDashboardDto
        {
            HotelId = hotelId,
            Date = day,
            TotalRooms = rooms.Count,
            OccupiedRooms = occupied,
            OccupancyPercent = Percent(occupied, rooms.Count),
            Arrivals = arrivals.Count,
            ArrivalsCheckedIn = arrivals.Count(r => r.Status == ReservationStatus.CheckedIn),
            Departures = departures.Count,
            DeparturesCheckedOut = departures.Count(r => r.Status == ReservationStatus.CheckedOut),
            InHouse = inHouse.Count,
            RevenueToday = await RevenueAsync(hotelId, day, utcOffsetMinutes),
            PendingApprovals = reservations.Count(r => r.Status == ReservationStatus.Pending),
            OpenHousekeepingTasks = openTasks,
            RoomsByStatus = rooms
                .GroupBy(r => r.Status)
                .OrderBy(g => g.Key)
                .Select(g => new RoomStatusCountDto { Status = g.Key, Count = g.Count() })
                .ToList(),
            Attention = Attention(reservations, day, tomorrow)
        };
    }

    private static List<AttentionItemDto> Attention(List<Reservation> reservations, DateTime day, DateTime tomorrow)
    {
        static string Guest(Reservation r) => $"{r.Guest.FirstName} {r.Guest.LastName}".Trim();

        var overdue = reservations
            .Where(r => r.Status == ReservationStatus.Confirmed && r.CheckInDate < day)
            .OrderBy(r => r.CheckInDate)
            .Select(r => new AttentionItemDto
            {
                Kind = AttentionKinds.OverdueArrival,
                ReservationId = r.Id,
                RoomId = r.RoomId,
                Title = Guest(r),
                Detail = $"Was due {r.CheckInDate:d MMM} · Room {r.Room.RoomNumber}"
            });

        var unpaid = reservations
            .Where(r => r.Status == ReservationStatus.CheckedIn && r.CheckOutDate < tomorrow && r.RemainingAmount > 0)
            .OrderBy(r => r.CheckOutDate)
            .Select(r => new AttentionItemDto
            {
                Kind = AttentionKinds.UnpaidDeparture,
                ReservationId = r.Id,
                RoomId = r.RoomId,
                Title = Guest(r),
                Detail = $"Leaving today · Room {r.Room.RoomNumber}",
                Amount = r.RemainingAmount
            });

        var notReady = reservations
            .Where(r => r.Status == ReservationStatus.Confirmed && r.CheckInDate >= day && r.CheckInDate < tomorrow
                && r.Room.Status is RoomStatus.Cleaning or RoomStatus.Maintenance or RoomStatus.OutOfService or RoomStatus.Occupied)
            .OrderBy(r => r.CheckInDate)
            .Select(r => new AttentionItemDto
            {
                Kind = AttentionKinds.RoomNotReady,
                ReservationId = r.Id,
                RoomId = r.RoomId,
                Title = $"Room {r.Room.RoomNumber}",
                Detail = $"{Guest(r)} arrives today; the room is {RoomStatusText(r.Room.Status)}"
            });

        var pending = reservations
            .Where(r => r.Status == ReservationStatus.Pending)
            .OrderBy(r => r.CheckInDate)
            .Select(r => new AttentionItemDto
            {
                Kind = AttentionKinds.PendingApproval,
                ReservationId = r.Id,
                RoomId = r.RoomId,
                Title = Guest(r),
                Detail = $"Wants {r.CheckInDate:d MMM} · Room {r.Room.RoomNumber}"
            });

        return overdue.Concat(unpaid).Concat(notReady).Concat(pending).Take(MaxAttentionItems).ToList();
    }

    private static string RoomStatusText(RoomStatus status) => status switch
    {
        RoomStatus.Cleaning => "waiting to be cleaned",
        RoomStatus.Maintenance => "in maintenance",
        RoomStatus.OutOfService => "out of service",
        RoomStatus.Occupied => "still occupied",
        _ => status.ToString().ToLowerInvariant()
    };

    public async Task<List<DailyTrendDto>> GetTrendAsync(int hotelId, DateTime date, int days, int utcOffsetMinutes)
    {
        var last = date.Date;
        var first = last.AddDays(1 - days);

        var totalRooms = await _context.Rooms.CountAsync(r => r.HotelId == hotelId && r.IsActive);

        // Overnight stays that were actually slept in during the period
        var stays = await _context.Reservations
            .Where(r => r.HotelId == hotelId
                && r.BookingType == BookingType.Daily
                && (r.Status == ReservationStatus.CheckedIn || r.Status == ReservationStatus.CheckedOut)
                && r.CheckInDate < last.AddDays(1) && r.CheckOutDate > first)
            .Select(r => new { r.RoomId, r.CheckInDate, r.CheckOutDate })
            .ToListAsync();

        var payments = await PaymentsAsync(hotelId, first, last.AddDays(1), utcOffsetMinutes);

        return Enumerable.Range(0, days)
            .Select(i =>
            {
                var d = first.AddDays(i);
                // A room counts for a night when a stay covers it: in on or before, out after
                var occupied = stays
                    .Where(s => s.CheckInDate.Date <= d && s.CheckOutDate.Date > d)
                    .Select(s => s.RoomId)
                    .Distinct()
                    .Count();
                return new DailyTrendDto
                {
                    Date = d,
                    Revenue = payments.Where(p => p.LocalDay == d).Sum(p => p.Net),
                    OccupiedRooms = occupied,
                    TotalRooms = totalRooms,
                    OccupancyPercent = Percent(occupied, totalRooms)
                };
            })
            .ToList();
    }

    private async Task<decimal> RevenueAsync(int hotelId, DateTime day, int utcOffsetMinutes) =>
        (await PaymentsAsync(hotelId, day, day.AddDays(1), utcOffsetMinutes)).Sum(p => p.Net);

    /// <summary>Payments (positive) and refunds (negative) made between two hotel-local days</summary>
    private async Task<List<(DateTime LocalDay, decimal Net)>> PaymentsAsync(int hotelId, DateTime fromDay, DateTime toDay, int utcOffsetMinutes)
    {
        var fromUtc = fromDay.AddMinutes(-utcOffsetMinutes);
        var toUtc = toDay.AddMinutes(-utcOffsetMinutes);
        var rows = await _context.Payments
            .Where(p => p.Reservation.HotelId == hotelId && p.CreatedAt >= fromUtc && p.CreatedAt < toUtc)
            .Select(p => new { p.CreatedAt, p.Amount, p.Type })
            .ToListAsync();
        return rows
            .Select(p => (p.CreatedAt.AddMinutes(utcOffsetMinutes).Date,
                p.Type == PaymentTransactionType.Refund ? -p.Amount : p.Amount))
            .ToList();
    }

    private static double Percent(int part, int whole) =>
        whole == 0 ? 0 : Math.Round(100.0 * part / whole, 1);
}
