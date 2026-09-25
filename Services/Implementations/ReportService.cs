using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace HotelManagement.Services.Implementations
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHotelAccessService _hotelAccess;

        public ReportService(ApplicationDbContext context, IHotelAccessService hotelAccess)
        {
            _context = context;
            _hotelAccess = hotelAccess;
        }

        private async Task<IReadOnlyList<int>> GetAccessibleHotelIdsAsync() =>
            await _hotelAccess.GetAccessibleHotelIdsAsync();

        public async Task<IEnumerable<DailyRevenueDto>> GetDailyRevenueAsync(DateTime startDate, DateTime endDate)
        {
            var hotelIds = await GetAccessibleHotelIdsAsync();

            var reservations = await _context.Reservations
                .Where(r => hotelIds.Contains(r.HotelId) &&
                            r.CheckInDate >= startDate && r.CheckInDate <= endDate &&
                            r.Status == ReservationStatus.CheckedOut)
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
            var hotelIds = await GetAccessibleHotelIdsAsync();

            var reservations = await _context.Reservations
                .Where(r => hotelIds.Contains(r.HotelId) &&
                            r.CheckInDate >= startDate && r.CheckInDate <= endDate &&
                            r.Status == ReservationStatus.CheckedOut)
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
            var hotelIds = await GetAccessibleHotelIdsAsync();

            var reservations = await _context.Reservations
                .Where(r => hotelIds.Contains(r.HotelId) &&
                            r.CheckInDate.Year == year &&
                            r.Status == ReservationStatus.CheckedOut)
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
            var hotelIds = await GetAccessibleHotelIdsAsync();
            var totalRooms = await _context.Rooms.CountAsync(r => hotelIds.Contains(r.HotelId));

            var occupancy = await _context.Reservations
                .Where(r => hotelIds.Contains(r.HotelId) &&
                            r.CheckInDate >= startDate && r.CheckInDate <= endDate &&
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
            var hotelIds = await GetAccessibleHotelIdsAsync();

            var guestHistory = await _context.Guests
                .Where(g => g.HotelId.HasValue && hotelIds.Contains(g.HotelId.Value) ||
                            g.Reservations.Any(r => hotelIds.Contains(r.HotelId)))
                .Select(g => new GuestVisitHistoryDto
                {
                    GuestId = g.Id,
                    GuestName = g.FirstName + " " + g.LastName,
                    VisitCount = g.Reservations.Count(r => hotelIds.Contains(r.HotelId) && r.Status == ReservationStatus.CheckedOut),
                    TotalSpent = g.Reservations
                        .Where(r => hotelIds.Contains(r.HotelId) && r.Status == ReservationStatus.CheckedOut)
                        .Sum(r => r.TotalAmount),
                    LastVisit = g.Reservations
                        .Where(r => hotelIds.Contains(r.HotelId))
                        .OrderByDescending(r => r.CheckOutDate)
                        .Select(r => r.CheckOutDate)
                        .FirstOrDefault()
                })
                .OrderByDescending(g => g.TotalSpent)
                .Take(50)
                .ToListAsync();

            return guestHistory;
        }

        public async Task<IEnumerable<OutstandingPaymentDto>> GetOutstandingPaymentsAsync()
        {
            var hotelIds = await GetAccessibleHotelIdsAsync();

            var outstandingPayments = await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Where(r => hotelIds.Contains(r.HotelId) &&
                            r.RemainingAmount > 0 &&
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
                    PaidAmount = r.DepositAmount,
                    RemainingAmount = r.RemainingAmount,
                    Status = r.Status.ToString()
                })
                .OrderByDescending(r => r.RemainingAmount)
                .ToListAsync();

            return outstandingPayments;
        }

        public async Task<IEnumerable<CancellationReportDto>> GetCancellationsAsync(DateTime startDate, DateTime endDate)
        {
            var hotelIds = await GetAccessibleHotelIdsAsync();
            // CancelledAt is a timestamp; the end date covers that whole day
            var endExclusive = endDate.Date.AddDays(1);

            var cancellations = await _context.Reservations
                .Where(r => hotelIds.Contains(r.HotelId) &&
                            r.CancelledAt.HasValue &&
                            r.CancelledAt.Value >= startDate &&
                            r.CancelledAt.Value < endExclusive &&
                            r.Status == ReservationStatus.Cancelled)
                .Select(r => new { r.CancelledAt, r.TotalAmount, r.CancellationReason })
                .ToListAsync();

            return cancellations
                .GroupBy(r => r.CancelledAt!.Value.Date)
                .Select(g => new CancellationReportDto
                {
                    Date = g.Key,
                    CancellationCount = g.Count(),
                    LostRevenue = g.Sum(r => r.TotalAmount),
                    MostCommonReason = g
                        .GroupBy(r => r.CancellationReason ?? "No reason")
                        .OrderByDescending(gr => gr.Count())
                        .Select(gr => gr.Key)
                        .FirstOrDefault() ?? "N/A"
                })
                .OrderBy(c => c.Date);
        }

        public async Task<IEnumerable<NoShowReportDto>> GetNoShowsAsync(DateTime startDate, DateTime endDate)
        {
            var hotelIds = await GetAccessibleHotelIdsAsync();

            return await _context.Reservations
                .Include(r => r.Guest)
                .Include(r => r.Room)
                .Where(r => hotelIds.Contains(r.HotelId) &&
                            r.Status == ReservationStatus.NoShow &&
                            r.CheckInDate >= startDate &&
                            r.CheckInDate <= endDate)
                .Select(r => new NoShowReportDto
                {
                    ReservationId = r.Id,
                    GuestName = r.Guest.FirstName + " " + r.Guest.LastName,
                    RoomNumber = r.Room.RoomNumber,
                    CheckInDate = r.CheckInDate,
                    TotalAmount = r.TotalAmount
                })
                .OrderByDescending(r => r.CheckInDate)
                .ToListAsync();
        }

        /// <summary>
        /// Money actually received per day and method, from the payments ledger.
        /// Refunds count as negative amounts on the day they were paid out.
        /// </summary>
        public async Task<IEnumerable<PaymentReconciliationDto>> GetPaymentReconciliationAsync(DateTime startDate, DateTime endDate)
        {
            var hotelIds = await GetAccessibleHotelIdsAsync();
            var endExclusive = endDate.Date.AddDays(1);

            var payments = await _context.Payments
                .Where(p => hotelIds.Contains(p.Reservation.HotelId) &&
                            p.CreatedAt >= startDate &&
                            p.CreatedAt < endExclusive)
                .Select(p => new
                {
                    p.CreatedAt,
                    p.Method,
                    SignedAmount = p.Type == PaymentTransactionType.Payment ? p.Amount : -p.Amount
                })
                .ToListAsync();

            return payments
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new PaymentReconciliationDto
                {
                    Date = g.Key,
                    TotalTransactions = g.Count(),
                    CashRevenue = g.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.SignedAmount),
                    CardRevenue = g.Where(p => p.Method is PaymentMethod.CreditCard or PaymentMethod.DebitCard).Sum(p => p.SignedAmount),
                    BankTransferRevenue = g.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.SignedAmount),
                    OtherRevenue = g.Where(p => p.Method is not (PaymentMethod.Cash or PaymentMethod.CreditCard or PaymentMethod.DebitCard or PaymentMethod.BankTransfer)).Sum(p => p.SignedAmount),
                    TotalRevenue = g.Sum(p => p.SignedAmount)
                })
                .OrderBy(r => r.Date)
                .ToList();
        }
    }
}
