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
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ReportService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private string? GetCurrentUserId() =>
            _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        private bool IsSuperAdmin() =>
            _httpContextAccessor.HttpContext?.User.IsInRole("SuperAdmin") ?? false;

        /// <summary>
        /// Returns hotel IDs accessible to the current user.
        /// SuperAdmin sees all hotels; everyone else sees only hotels they own.
        /// </summary>
        private async Task<List<int>> GetAccessibleHotelIdsAsync()
        {
            if (IsSuperAdmin())
                return await _context.Hotels.Select(h => h.Id).ToListAsync();

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return new List<int>();

            return await _context.Hotels
                .Where(h => h.OwnerId == userId)
                .Select(h => h.Id)
                .ToListAsync();
        }

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

            var cancellations = await _context.Reservations
                .Where(r => hotelIds.Contains(r.HotelId) &&
                            r.CancelledAt.HasValue &&
                            r.CancelledAt.Value >= startDate &&
                            r.CancelledAt.Value <= endDate &&
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

        public async Task<IEnumerable<PaymentReconciliationDto>> GetPaymentReconciliationAsync(DateTime startDate, DateTime endDate)
        {
            var hotelIds = await GetAccessibleHotelIdsAsync();

            var payments = await _context.Reservations
                .Where(r => hotelIds.Contains(r.HotelId) &&
                            r.UpdatedAt.HasValue &&
                            r.UpdatedAt.Value >= startDate &&
                            r.UpdatedAt.Value <= endDate &&
                            r.PaymentStatus != PaymentStatus.Unpaid)
                .Select(r => new { r.UpdatedAt, r.TotalAmount, r.PaymentMethod })
                .ToListAsync();

            return payments
                .GroupBy(r => r.UpdatedAt!.Value.Date)
                .Select(g => new PaymentReconciliationDto
                {
                    Date = g.Key,
                    TotalTransactions = g.Count(),
                    CashRevenue = g.Where(r => r.PaymentMethod == PaymentMethod.Cash).Sum(r => r.TotalAmount),
                    CardRevenue = g.Where(r => r.PaymentMethod == PaymentMethod.CreditCard || r.PaymentMethod == PaymentMethod.DebitCard).Sum(r => r.TotalAmount),
                    BankTransferRevenue = g.Where(r => r.PaymentMethod == PaymentMethod.BankTransfer).Sum(r => r.TotalAmount),
                    OtherRevenue = g.Where(r => r.PaymentMethod != PaymentMethod.Cash &&
                                                r.PaymentMethod != PaymentMethod.CreditCard &&
                                                r.PaymentMethod != PaymentMethod.DebitCard &&
                                                r.PaymentMethod != PaymentMethod.BankTransfer).Sum(r => r.TotalAmount),
                    TotalRevenue = g.Sum(r => r.TotalAmount)
                })
                .OrderBy(r => r.Date);
        }
    }
}
