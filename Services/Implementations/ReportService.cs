using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HotelManagement.Services.Implementations
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DailyRevenueDto>> GetDailyRevenueAsync(DateTime startDate, DateTime endDate)
        {
            var reservations = await _context.Reservations
                .Where(r => r.CheckInDate >= startDate && r.CheckInDate <= endDate && r.Status == ReservationStatus.CheckedOut)
                .GroupBy(r => r.CheckInDate.Date)
                .Select(g => new DailyRevenueDto
                {
                    Date = g.Key,
                    TotalRevenue = g.Sum(r => r.TotalAmount),
                    ReservationCount = g.Count()
                })
                .OrderBy(r => r.Date)
                .ToListAsync();

            return reservations;
        }

        public async Task<IEnumerable<WeeklyRevenueDto>> GetWeeklyRevenueAsync(DateTime startDate, DateTime endDate)
        {
            // Note: EF Core might not translate complex date grouping well, so we fetch relevant data first
            // For larger datasets, a stored procedure or raw SQL would be better
            var reservations = await _context.Reservations
                .Where(r => r.CheckInDate >= startDate && r.CheckInDate <= endDate && r.Status == ReservationStatus.CheckedOut)
                .Select(r => new { r.CheckInDate, r.TotalAmount })
                .ToListAsync();

            var weeklyData = reservations
                .GroupBy(r => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(r.CheckInDate, CalendarWeekRule.FirstDay, DayOfWeek.Monday))
                .Select(g => {
                    var firstDate = g.Min(x => x.CheckInDate);
                    var lastDate = g.Max(x => x.CheckInDate);
                    return new WeeklyRevenueDto
                    {
                        WeekStart = firstDate.ToString("yyyy-MM-dd"),
                        WeekEnd = lastDate.ToString("yyyy-MM-dd"),
                        TotalRevenue = g.Sum(r => r.TotalAmount)
                    };
                })
                .OrderBy(w => w.WeekStart);

            return weeklyData;
        }

        public async Task<IEnumerable<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int year)
        {
            var reservations = await _context.Reservations
                .Where(r => r.CheckInDate.Year == year && r.Status == ReservationStatus.CheckedOut)
                .GroupBy(r => r.CheckInDate.Month)
                .Select(g => new MonthlyRevenueDto
                {
                    Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key),
                    Year = year,
                    TotalRevenue = g.Sum(r => r.TotalAmount),
                    ReservationCount = g.Count()
                })
                .ToListAsync();

            return reservations;
        }

        public async Task<IEnumerable<OccupancyReportDto>> GetOccupancyHistoryAsync(DateTime startDate, DateTime endDate)
        {
            var totalRooms = await _context.Rooms.CountAsync();
            
            // This is a simplified occupancy calculation based on check-ins
            // A more accurate one would check date ranges overlap
            var occupancy = await _context.Reservations
                .Where(r => r.CheckInDate >= startDate && r.CheckInDate <= endDate && 
                           (r.Status == ReservationStatus.CheckedIn || 
                            r.Status == ReservationStatus.CheckedOut))
                .GroupBy(r => r.CheckInDate.Date)
                .Select(g => new OccupancyReportDto
                {
                    Date = g.Key,
                    TotalRooms = totalRooms,
                    OccupiedRooms = g.Count(),
                    OccupancyRate = totalRooms > 0 ? (double)g.Count() / totalRooms * 100 : 0
                })
                .OrderBy(o => o.Date)
                .ToListAsync();

            return occupancy;
        }

        public async Task<IEnumerable<GuestVisitHistoryDto>> GetGuestVisitHistoryAsync()
        {
            var guestHistory = await _context.Guests
                .Select(g => new GuestVisitHistoryDto
                {
                    GuestId = g.Id,
                    GuestName = g.FirstName + " " + g.LastName,
                    VisitCount = g.Reservations.Count(r => r.Status == ReservationStatus.CheckedOut),
                    TotalSpent = g.Reservations
                        .Where(r => r.Status == ReservationStatus.CheckedOut)
                        .Sum(r => r.TotalAmount),
                    LastVisit = g.Reservations
                        .OrderByDescending(r => r.CheckOutDate)
                        .Select(r => r.CheckOutDate)
                        .FirstOrDefault()
                })
                .OrderByDescending(g => g.TotalSpent)
                .Take(50) // Top 50 guests
                .ToListAsync();

            return guestHistory;
        }

        public async Task<IEnumerable<OutstandingPaymentDto>> GetOutstandingPaymentsAsync()
        {
            var outstandingPayments = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Where(r => r.RemainingAmount > 0 && 
                            r.Status != ReservationStatus.Cancelled && 
                            r.Status != ReservationStatus.NoShow)
                .Select(r => new OutstandingPaymentDto
                {
                    ReservationId = r.Id,
                    GuestName = r.Guest.FirstName + " " + r.Guest.LastName,
                    RoomNumber = r.Room.RoomNumber,
                    CheckInDate = r.CheckInDate,
                    CheckOutDate = r.CheckOutDate,
                    TotalAmount = r.TotalAmount,
                    PaidAmount = r.DepositAmount, // Assuming DepositAmount tracks what has been paid so far
                    RemainingAmount = r.RemainingAmount,
                    Status = r.Status.ToString()
                })
                .OrderByDescending(r => r.RemainingAmount)
                .ToListAsync();

            return outstandingPayments;
        }
    }
}
