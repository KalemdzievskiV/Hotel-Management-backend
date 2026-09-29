using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces;

public interface IDashboardService
{
    /// <param name="date">The hotel's calendar day</param>
    /// <param name="utcOffsetMinutes">The hotel's offset from UTC, for payments (stamped in UTC) made "today"</param>
    Task<TodayDashboardDto> GetTodayAsync(int hotelId, DateTime date, int utcOffsetMinutes);

    /// <summary>The <paramref name="days"/> days up to and including <paramref name="date"/>, oldest first</summary>
    Task<List<DailyTrendDto>> GetTrendAsync(int hotelId, DateTime date, int days, int utcOffsetMinutes);
}
