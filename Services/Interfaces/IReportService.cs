using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces
{
    public interface IReportService
    {
        Task<IEnumerable<DailyRevenueDto>> GetDailyRevenueAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<WeeklyRevenueDto>> GetWeeklyRevenueAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int year);
        Task<IEnumerable<OccupancyReportDto>> GetOccupancyHistoryAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<GuestVisitHistoryDto>> GetGuestVisitHistoryAsync();
        Task<IEnumerable<OutstandingPaymentDto>> GetOutstandingPaymentsAsync();
    }
}
