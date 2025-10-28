using AutoMapper;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Repositories.Interfaces;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

public class HotelService : CrudService<Hotel, HotelDto>, IHotelService
{
    private readonly IGenericRepository<Hotel> _hotelRepository;
    private readonly IMapper _mapper;

    public HotelService(
        IGenericRepository<Hotel> repository,
        IMapper mapper) : base(repository, mapper)
    {
        _hotelRepository = repository;
        _mapper = mapper;
    }

    public override async Task<HotelDto> CreateAsync(HotelDto dto)
    {
        // OwnerId must be set by the controller from authenticated user
        if (string.IsNullOrEmpty(dto.OwnerId))
        {
            throw new InvalidOperationException("OwnerId must be set before creating a hotel");
        }

        var hotel = _mapper.Map<Hotel>(dto);
        hotel.CreatedAt = DateTime.UtcNow;
        
        await _hotelRepository.AddAsync(hotel);
        await _hotelRepository.SaveAsync();
        
        return _mapper.Map<HotelDto>(hotel);
    }

    public override async Task<HotelDto> UpdateAsync(int id, HotelDto dto)
    {
        var existingHotel = await _hotelRepository.GetByIdAsync(id);
        
        if (existingHotel == null)
        {
            throw new KeyNotFoundException($"Hotel with ID {id} not found");
        }

        // Don't allow changing the owner
        dto.OwnerId = existingHotel.OwnerId;
        
        _mapper.Map(dto, existingHotel);
        existingHotel.UpdatedAt = DateTime.UtcNow;
        
        _hotelRepository.Update(existingHotel);
        await _hotelRepository.SaveAsync();
        
        return _mapper.Map<HotelDto>(existingHotel);
    }

    public override async Task DeleteAsync(int id)
    {
        var hotel = await _hotelRepository.GetByIdAsync(id);
        
        if (hotel == null)
        {
            throw new KeyNotFoundException($"Hotel with ID {id} not found");
        }

        try
        {
            _hotelRepository.Delete(hotel);
            await _hotelRepository.SaveAsync();
        }
        catch (DbUpdateException ex)
        {
            // Check if it's a foreign key constraint violation
            if (ex.InnerException?.Message.Contains("FK_") == true || 
                ex.InnerException?.Message.Contains("REFERENCE constraint") == true)
            {
                throw new InvalidOperationException(
                    "Cannot delete this hotel because it has related data (rooms, guests, or reservations). " +
                    "Please remove all associated data before deleting the hotel.");
            }
            
            throw; // Re-throw if it's a different type of error
        }
    }

    public async Task<IEnumerable<HotelDto>> GetHotelsByOwnerAsync(string ownerId)
    {
        var hotels = await _hotelRepository.FindAsync(h => h.OwnerId == ownerId);
        return _mapper.Map<IEnumerable<HotelDto>>(hotels);
    }

    public async Task<IEnumerable<HotelDto>> GetAllHotelsForUserAsync(string userId, bool isSuperAdmin)
    {
        IEnumerable<Hotel> hotels;
        
        if (isSuperAdmin)
        {
            // SuperAdmin sees all hotels
            hotels = await _hotelRepository.GetAllAsync();
        }
        else
        {
            // Regular admins see only their hotels
            hotels = await _hotelRepository.FindAsync(h => h.OwnerId == userId);
        }
        
        return _mapper.Map<IEnumerable<HotelDto>>(hotels);
    }
}
