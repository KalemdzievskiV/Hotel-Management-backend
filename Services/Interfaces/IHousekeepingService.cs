using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces;

public interface IHousekeepingService
{
    Task<IEnumerable<HousekeepingTaskDto>> GetTasksAsync(int hotelId, DateTime? date, string? status);
    Task<HousekeepingTaskDto?> GetTaskByIdAsync(int id);
    Task<HousekeepingTaskDto> CreateTaskAsync(CreateHousekeepingTaskDto dto, string userId);
    Task<HousekeepingTaskDto> UpdateTaskAsync(int id, UpdateHousekeepingTaskDto dto);
    Task DeleteTaskAsync(int id);
    Task<HousekeepingTaskDto> StartTaskAsync(int id);
    Task<HousekeepingTaskDto> CompleteTaskAsync(int id);
    Task<HousekeepingScheduleDto> GetScheduleAsync(int hotelId, DateTime date);
    Task<IEnumerable<HousekeeperPerformanceDto>> GetPerformanceAsync(int hotelId, DateTime from, DateTime to);
    Task GenerateDailyTasksAsync(int hotelId, DateTime date, string userId);
}
