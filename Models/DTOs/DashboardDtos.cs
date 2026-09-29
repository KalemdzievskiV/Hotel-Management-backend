using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// Everything the front desk's "Today" screen shows for one hotel, in one call
/// </summary>
public class TodayDashboardDto
{
    public int HotelId { get; set; }
    /// <summary>The hotel's calendar day the numbers are for</summary>
    public DateTime Date { get; set; }

    public int TotalRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public double OccupancyPercent { get; set; }

    /// <summary>Stays starting today that are expected (pending, confirmed) or already here</summary>
    public int Arrivals { get; set; }
    public int ArrivalsCheckedIn { get; set; }
    /// <summary>Stays ending today, still in the room or already gone</summary>
    public int Departures { get; set; }
    public int DeparturesCheckedOut { get; set; }
    public int InHouse { get; set; }

    /// <summary>Payments minus refunds recorded today</summary>
    public decimal RevenueToday { get; set; }
    public int PendingApprovals { get; set; }
    public int OpenHousekeepingTasks { get; set; }

    public List<RoomStatusCountDto> RoomsByStatus { get; set; } = new();

    /// <summary>What someone at the desk should deal with, most urgent first</summary>
    public List<AttentionItemDto> Attention { get; set; } = new();
}

public class RoomStatusCountDto
{
    public RoomStatus Status { get; set; }
    public int Count { get; set; }
}

public static class AttentionKinds
{
    /// <summary>A confirmed guest whose check-in day has passed: check in or mark as no-show</summary>
    public const string OverdueArrival = "overdueArrival";
    /// <summary>A guest leaving today who still owes money</summary>
    public const string UnpaidDeparture = "unpaidDeparture";
    /// <summary>A guest arriving today whose room isn't ready</summary>
    public const string RoomNotReady = "roomNotReady";
    /// <summary>An online booking waiting to be confirmed</summary>
    public const string PendingApproval = "pendingApproval";
}

public class AttentionItemDto
{
    public string Kind { get; set; } = string.Empty;
    public int? ReservationId { get; set; }
    public int? RoomId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
}

/// <summary>One day of the recent trend: money taken and rooms slept in</summary>
public class DailyTrendDto
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int OccupiedRooms { get; set; }
    public int TotalRooms { get; set; }
    public double OccupancyPercent { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore => Page * PageSize < TotalCount;
}
