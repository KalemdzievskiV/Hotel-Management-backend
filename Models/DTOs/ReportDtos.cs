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
}
