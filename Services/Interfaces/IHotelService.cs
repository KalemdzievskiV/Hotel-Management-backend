using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces;

public interface IHotelService : ICrudService<HotelDto>
{
    Task<IEnumerable<HotelDto>> GetHotelsByOwnerAsync(string ownerId);
    Task<IEnumerable<HotelDto>> GetAllHotelsForUserAsync(string userId, bool isSuperAdmin);
    Task<IEnumerable<HotelDto>> GetAllHotelsUnfilteredAsync();
}
