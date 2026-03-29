namespace HotelManagement.Models.DTOs
{
    public class DailyRevenueDto
    {
        public DateTime Date { get; set; }
        public decimal TotalRevenue { get; set; }
        public int ReservationCount { get; set; }
    }

    public class WeeklyRevenueDto
    {
        public string WeekStart { get; set; }
        public string WeekEnd { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class MonthlyRevenueDto
    {
        public string Month { get; set; }
        public int Year { get; set; }
        public decimal TotalRevenue { get; set; }
        public int ReservationCount { get; set; }
    }

    public class OccupancyReportDto
    {
        public DateTime Date { get; set; }
        public int TotalRooms { get; set; }
        public int OccupiedRooms { get; set; }
        public double OccupancyRate { get; set; }
    }

    public class GuestVisitHistoryDto
    {
        public int GuestId { get; set; }
        public string GuestName { get; set; }
        public int VisitCount { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime LastVisit { get; set; }
    }

    public class CancellationReportDto
    {
        public DateTime Date { get; set; }
        public int CancellationCount { get; set; }
        public decimal LostRevenue { get; set; }
        public string MostCommonReason { get; set; }
    }

    public class NoShowReportDto
    {
        public int ReservationId { get; set; }
        public string GuestName { get; set; }
        public string RoomNumber { get; set; }
        public DateTime CheckInDate { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class PaymentReconciliationDto
    {
        public DateTime Date { get; set; }
        public int TotalTransactions { get; set; }
        public decimal CashRevenue { get; set; }
        public decimal CardRevenue { get; set; }
        public decimal BankTransferRevenue { get; set; }
        public decimal OtherRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
