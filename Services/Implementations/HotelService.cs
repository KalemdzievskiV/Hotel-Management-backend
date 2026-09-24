using AutoMapper;
using AutoMapper.QueryableExtensions;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Exceptions;
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
    private readonly ApplicationDbContext _context;
    private readonly IHotelAccessService _hotelAccess;

    public HotelService(
        IGenericRepository<Hotel> repository,
        IMapper mapper,
        ApplicationDbContext context,
        IHotelAccessService hotelAccess) : base(repository, mapper)
    {
        _hotelRepository = repository;
        _mapper = mapper;
        _context = context;
        _hotelAccess = hotelAccess;
    }

    /// <summary>
    /// Hotels the current user can access (owned, assigned, or all for SuperAdmin)
    /// </summary>
    public override async Task<IEnumerable<HotelDto>> GetAllAsync()
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync();

        return await _context.Hotels
            .Where(h => hotelIds.Contains(h.Id))
            .ProjectTo<HotelDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public override async Task<HotelDto?> GetByIdAsync(int id)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(id))
            return null;

        return await _context.Hotels
            .Where(h => h.Id == id)
            .ProjectTo<HotelDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
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
        
        // Reload with Owner to map OwnerName
        var createdHotel = await _context.Hotels
            .Include(h => h.Owner)
            .FirstOrDefaultAsync(h => h.Id == hotel.Id);
        
        return _mapper.Map<HotelDto>(createdHotel ?? hotel);
    }

    public override async Task<HotelDto> UpdateAsync(int id, HotelDto dto)
    {
        var existingHotel = await _context.Hotels
            .Include(h => h.Owner)
            .FirstOrDefaultAsync(h => h.Id == id);
        
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
                throw new BusinessRuleException(
                    "Cannot delete this hotel because it has related data (rooms, guests, or reservations). " +
                    "Please remove all associated data before deleting the hotel.");
            }
            
            throw; // Re-throw if it's a different type of error
        }
    }

    public async Task<IEnumerable<HotelDto>> GetHotelsByOwnerAsync(string ownerId)
    {
        return await _context.Hotels
            .Where(h => h.OwnerId == ownerId)
            .ProjectTo<HotelDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IEnumerable<HotelDto>> GetAllHotelsForUserAsync(string userId, bool isSuperAdmin)
    {
        IQueryable<Hotel> query = _context.Hotels.Include(h => h.Owner);
        
        if (!isSuperAdmin)
        {
            // Regular admins see only their hotels
            query = query.Where(h => h.OwnerId == userId);
        }
        
        return await query.ProjectTo<HotelDto>(_mapper.ConfigurationProvider).ToListAsync();
    }

    /// <summary>
    /// Gets all hotels without any ownership filtering
    /// Used for public browsing (guest users looking for available hotels)
    /// </summary>
    /// <summary>
    /// Single hotel without access filtering, for public browsing by guests
    /// </summary>
    public async Task<HotelDto?> GetByIdUnfilteredAsync(int id)
    {
        return await _context.Hotels
            .Where(h => h.Id == id)
            .ProjectTo<HotelDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<HotelDto>> GetAllHotelsUnfilteredAsync()
    {
        return await _context.Hotels
            .ProjectTo<HotelDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}
