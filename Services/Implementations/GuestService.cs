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

public class GuestService : CrudService<Guest, GuestDto>, IGuestService
{
    private readonly IGenericRepository<Guest> _guestRepository;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;

    public GuestService(
        IGenericRepository<Guest> repository, 
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext context)
        : base(repository, mapper)
    {
        _guestRepository = repository;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public override async Task<GuestDto> CreateAsync(GuestDto dto)
    {
        // Validate email uniqueness
        var isUnique = await IsEmailUniqueAsync(dto.Email);
        if (!isUnique)
        {
            throw new InvalidOperationException($"A guest with email '{dto.Email}' already exists");
        }

        var guest = _mapper.Map<Guest>(dto);
        guest.CreatedAt = DateTime.UtcNow;
        
        // If this is a walk-in guest (UserId is null), set ownership
        if (string.IsNullOrEmpty(dto.UserId))
        {
            var currentUserId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(currentUserId))
            {
                guest.CreatedByUserId = currentUserId;
            }
            
            // Set HotelId if provided
            if (dto.HotelId.HasValue)
            {
                guest.HotelId = dto.HotelId.Value;
            }
        }

        await _guestRepository.AddAsync(guest);
        await _guestRepository.SaveAsync();

        return _mapper.Map<GuestDto>(guest);
    }

    public override async Task<GuestDto> UpdateAsync(int id, GuestDto dto)
    {
        var existingGuest = await _guestRepository.GetByIdAsync(id);
        if (existingGuest == null)
            throw new KeyNotFoundException($"Guest with ID {id} not found");

        // Validate email uniqueness (excluding current guest)
        if (existingGuest.Email != dto.Email)
        {
            var isUnique = await IsEmailUniqueAsync(dto.Email, id);
            if (!isUnique)
            {
                throw new InvalidOperationException($"A guest with email '{dto.Email}' already exists");
            }
        }

        _mapper.Map(dto, existingGuest);
        existingGuest.UpdatedAt = DateTime.UtcNow;

        _guestRepository.Update(existingGuest);
        await _guestRepository.SaveAsync();

        return _mapper.Map<GuestDto>(existingGuest);
    }

    public async Task<IEnumerable<GuestDto>> SearchByNameAsync(string searchTerm)
    {
        var guests = await _guestRepository.FindAsync(g =>
            g.FirstName.Contains(searchTerm) ||
            g.LastName.Contains(searchTerm));

        return _mapper.Map<IEnumerable<GuestDto>>(guests);
    }

    public async Task<GuestDto?> GetByEmailAsync(string email)
    {
        var guests = await _guestRepository.FindAsync(g => g.Email == email);
        var guest = guests.FirstOrDefault();

        return guest == null ? null : _mapper.Map<GuestDto>(guest);
    }

    public async Task<GuestDto?> GetByPhoneNumberAsync(string phoneNumber)
    {
        var guests = await _guestRepository.FindAsync(g => g.PhoneNumber == phoneNumber);
        var guest = guests.FirstOrDefault();

        return guest == null ? null : _mapper.Map<GuestDto>(guest);
    }

    public async Task<GuestDto?> GetByUserIdAsync(string userId)
    {
        var guests = await _guestRepository.FindAsync(g => g.UserId == userId);
        var guest = guests.FirstOrDefault();

        return guest == null ? null : _mapper.Map<GuestDto>(guest);
    }

    public async Task<IEnumerable<GuestDto>> GetVIPGuestsAsync()
    {
        var guests = await _guestRepository.FindAsync(g => g.IsVIP && g.IsActive && !g.IsBlacklisted);
        return _mapper.Map<IEnumerable<GuestDto>>(guests);
    }

    public async Task<IEnumerable<GuestDto>> GetActiveGuestsAsync()
    {
        var guests = await _guestRepository.FindAsync(g => g.IsActive && !g.IsBlacklisted);
        return _mapper.Map<IEnumerable<GuestDto>>(guests);
    }

    public async Task<IEnumerable<GuestDto>> GetBlacklistedGuestsAsync()
    {
        var guests = await _guestRepository.FindAsync(g => g.IsBlacklisted);
        return _mapper.Map<IEnumerable<GuestDto>>(guests);
    }

    public async Task<bool> IsEmailUniqueAsync(string email, int? excludeGuestId = null)
    {
        var guests = await _guestRepository.FindAsync(g => g.Email == email);

        if (excludeGuestId.HasValue)
        {
            guests = guests.Where(g => g.Id != excludeGuestId.Value);
        }

        return !guests.Any();
    }

    public async Task BlacklistGuestAsync(int guestId, string reason)
    {
        var guest = await _guestRepository.GetByIdAsync(guestId);
        if (guest == null)
            throw new KeyNotFoundException($"Guest with ID {guestId} not found");

        guest.IsBlacklisted = true;
        guest.BlacklistReason = reason;
        guest.UpdatedAt = DateTime.UtcNow;

        _guestRepository.Update(guest);
        await _guestRepository.SaveAsync();
    }

    public async Task UnblacklistGuestAsync(int guestId)
    {
        var guest = await _guestRepository.GetByIdAsync(guestId);
        if (guest == null)
            throw new KeyNotFoundException($"Guest with ID {guestId} not found");

        guest.IsBlacklisted = false;
        guest.BlacklistReason = null;
        guest.UpdatedAt = DateTime.UtcNow;

        _guestRepository.Update(guest);
        await _guestRepository.SaveAsync();
    }

    public async Task SetVIPStatusAsync(int guestId, bool isVIP)
    {
        var guest = await _guestRepository.GetByIdAsync(guestId);
        if (guest == null)
            throw new KeyNotFoundException($"Guest with ID {guestId} not found");

        guest.IsVIP = isVIP;
        guest.UpdatedAt = DateTime.UtcNow;

        _guestRepository.Update(guest);
        await _guestRepository.SaveAsync();
    }

    public async Task<IEnumerable<GuestDto>> GetGuestsByHotelIdAsync(int hotelId)
    {
        var guests = await _guestRepository.FindAsync(g => g.HotelId == hotelId);
        return _mapper.Map<IEnumerable<GuestDto>>(guests);
    }

    public async Task<IEnumerable<GuestDto>> GetGuestsCreatedByUserAsync(string userId)
    {
        var guests = await _guestRepository.FindAsync(g => g.CreatedByUserId == userId);
        return _mapper.Map<IEnumerable<GuestDto>>(guests);
    }

    public async Task<IEnumerable<GuestDto>> GetMyAccessibleGuestsAsync(string currentUserId)
    {
        // Get walk-in guests created by this user + all registered users (UserId != null)
        var guests = await _guestRepository.FindAsync(g => 
            g.CreatedByUserId == currentUserId || // Walk-in guests I created
            g.UserId != null); // All registered users (available to everyone)
        
        return _mapper.Map<IEnumerable<GuestDto>>(guests);
    }

    public async Task<GuestDto> GetOrCreateGuestProfileAsync(string userId)
    {
        // Try to find existing guest by UserId
        var existingGuest = await _context.Guests
            .FirstOrDefaultAsync(g => g.UserId == userId);

        if (existingGuest != null)
        {
            return _mapper.Map<GuestDto>(existingGuest);
        }

        // Guest doesn't exist, create new one from user data
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found");
        }

        var newGuest = new Guest
        {
            UserId = userId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _guestRepository.AddAsync(newGuest);
        await _guestRepository.SaveAsync();

        return _mapper.Map<GuestDto>(newGuest);
    }
}
