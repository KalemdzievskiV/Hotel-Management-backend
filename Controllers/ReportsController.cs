using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin,Manager")] // Restrict to admin roles
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("revenue/daily")]
        public async Task<ActionResult<IEnumerable<DailyRevenueDto>>> GetDailyRevenue([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;
            var result = await _reportService.GetDailyRevenueAsync(start, end);
            return Ok(result);
        }

        [HttpGet("revenue/weekly")]
        public async Task<ActionResult<IEnumerable<WeeklyRevenueDto>>> GetWeeklyRevenue([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-90);
            var end = endDate ?? DateTime.Today;
            var result = await _reportService.GetWeeklyRevenueAsync(start, end);
            return Ok(result);
        }

        [HttpGet("revenue/monthly")]
        public async Task<ActionResult<IEnumerable<MonthlyRevenueDto>>> GetMonthlyRevenue([FromQuery] int? year)
        {
            var targetYear = year ?? DateTime.Today.Year;
            var result = await _reportService.GetMonthlyRevenueAsync(targetYear);
            return Ok(result);
        }

        [HttpGet("occupancy/history")]
        public async Task<ActionResult<IEnumerable<OccupancyReportDto>>> GetOccupancyHistory([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;
            var result = await _reportService.GetOccupancyHistoryAsync(start, end);
            return Ok(result);
        }

        [HttpGet("guests/history")]
        public async Task<ActionResult<IEnumerable<GuestVisitHistoryDto>>> GetGuestVisitHistory()
        {
            var result = await _reportService.GetGuestVisitHistoryAsync();
            return Ok(result);
        }

        [HttpGet("payments/outstanding")]
        public async Task<ActionResult<IEnumerable<OutstandingPaymentDto>>> GetOutstandingPayments()
        {
            var result = await _reportService.GetOutstandingPaymentsAsync();
            return Ok(result);
        }

        [HttpGet("payments/reconciliation")]
        public async Task<ActionResult<IEnumerable<PaymentReconciliationDto>>> GetPaymentReconciliation([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;
            var result = await _reportService.GetPaymentReconciliationAsync(start, end);
            return Ok(result);
        }

        [HttpGet("cancellations")]
        public async Task<ActionResult<IEnumerable<CancellationReportDto>>> GetCancellations([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;
            var result = await _reportService.GetCancellationsAsync(start, end);
            return Ok(result);
        }

        [HttpGet("noshows")]
        public async Task<ActionResult<IEnumerable<NoShowReportDto>>> GetNoShows([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;
            var result = await _reportService.GetNoShowsAsync(start, end);
            return Ok(result);
        }
    }
}
