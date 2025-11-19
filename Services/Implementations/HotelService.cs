using AutoMapper;
using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Repositories.Interfaces;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotelManagement.Services.Implementations;

public class HotelService : CrudService<Hotel, HotelDto>, IHotelService
{
    private readonly IGenericRepository<Hotel> _hotelRepository;
    private readonly IMapper _mapper;
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HotelService(
        IGenericRepository<Hotel> repository,
        IMapper mapper,
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor) : base(repository, mapper)
    {
        _hotelRepository = repository;
        _mapper = mapper;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the current user ID from the HTTP context
    /// </summary>
    private string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Checks if the current user is a SuperAdmin
    /// </summary>
    private bool IsSuperAdmin()
    {
        return _httpContextAccessor.HttpContext?.User.IsInRole("SuperAdmin") ?? false;
    }
    
    public override async Task<IEnumerable<HotelDto>> GetAllAsync()
    {
        var query = _context.Hotels.Include(h => h.Owner).AsQueryable();

        // Filter by ownership unless SuperAdmin
        if (!IsSuperAdmin())
        {
            var userId = GetCurrentUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                // Admin/Manager: Only see hotels they own
                query = query.Where(h => h.OwnerId == userId);
            }
            else
            {
                // No user context: return empty
                return Enumerable.Empty<HotelDto>();
            }
        }

        var hotels = await query.ToListAsync();
        return _mapper.Map<IEnumerable<HotelDto>>(hotels);
    }
    
    public override async Task<HotelDto?> GetByIdAsync(int id)
    {
        var query = _context.Hotels.Include(h => h.Owner).AsQueryable();

        // Filter by ownership unless SuperAdmin
        if (!IsSuperAdmin())
        {
            var userId = GetCurrentUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(h => h.OwnerId == userId);
            }
            else
            {
                return null;
            }
        }

        var hotel = await query.FirstOrDefaultAsync(h => h.Id == id);
        return hotel == null ? null : _mapper.Map<HotelDto>(hotel);
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
                throw new InvalidOperationException(
                    "Cannot delete this hotel because it has related data (rooms, guests, or reservations). " +
                    "Please remove all associated data before deleting the hotel.");
            }
            
            throw; // Re-throw if it's a different type of error
        }
    }

    public async Task<IEnumerable<HotelDto>> GetHotelsByOwnerAsync(string ownerId)
    {
        var hotels = await _context.Hotels
            .Include(h => h.Owner)
            .Where(h => h.OwnerId == ownerId)
            .ToListAsync();
        return _mapper.Map<IEnumerable<HotelDto>>(hotels);
    }

    public async Task<IEnumerable<HotelDto>> GetAllHotelsForUserAsync(string userId, bool isSuperAdmin)
    {
        IQueryable<Hotel> query = _context.Hotels.Include(h => h.Owner);
        
        if (!isSuperAdmin)
        {
            // Regular admins see only their hotels
            query = query.Where(h => h.OwnerId == userId);
        }
        
        var hotels = await query.ToListAsync();
        return _mapper.Map<IEnumerable<HotelDto>>(hotels);
    }

    /// <summary>
    /// Gets all hotels without any ownership filtering
    /// Used for public browsing (guest users looking for available hotels)
    /// </summary>
    public async Task<IEnumerable<HotelDto>> GetAllHotelsUnfilteredAsync()
    {
        var hotels = await _context.Hotels
            .Include(h => h.Owner)
            .ToListAsync();
        
        return _mapper.Map<IEnumerable<HotelDto>>(hotels);
    }
}
