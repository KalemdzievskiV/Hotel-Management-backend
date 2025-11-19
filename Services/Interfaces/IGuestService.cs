using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// Service interface for Guest-specific operations
/// </summary>
public interface IGuestService : ICrudService<GuestDto>
{
    /// <summary>
    /// Search guests by name (first name or last name)
    /// </summary>
    Task<IEnumerable<GuestDto>> SearchByNameAsync(string searchTerm);
    
    /// <summary>
    /// Search guests by email
    /// </summary>
    Task<GuestDto?> GetByEmailAsync(string email);
    
    /// <summary>
    /// Search guests by phone number
    /// </summary>
    Task<GuestDto?> GetByPhoneNumberAsync(string phoneNumber);
    
    /// <summary>
    /// Get guest by user ID (for registered users)
    /// </summary>
    Task<GuestDto?> GetByUserIdAsync(string userId);
    
    /// <summary>
    /// Get all VIP guests
    /// </summary>
    Task<IEnumerable<GuestDto>> GetVIPGuestsAsync();
    
    /// <summary>
    /// Get all active guests (not blacklisted)
    /// </summary>
    Task<IEnumerable<GuestDto>> GetActiveGuestsAsync();
    
    /// <summary>
    /// Get blacklisted guests
    /// </summary>
    Task<IEnumerable<GuestDto>> GetBlacklistedGuestsAsync();
    
    /// <summary>
    /// Check if email is unique (for new guest registration)
    /// </summary>
    Task<bool> IsEmailUniqueAsync(string email, int? excludeGuestId = null);
    
    /// <summary>
    /// Blacklist a guest
    /// </summary>
    Task BlacklistGuestAsync(int guestId, string reason);
    
    /// <summary>
    /// Remove guest from blacklist
    /// </summary>
    Task UnblacklistGuestAsync(int guestId);
    
    /// <summary>
    /// Mark guest as VIP
    /// </summary>
    Task SetVIPStatusAsync(int guestId, bool isVIP);
    
    /// <summary>
    /// Get all walk-in guests for a specific hotel
    /// </summary>
    Task<IEnumerable<GuestDto>> GetGuestsByHotelIdAsync(int hotelId);
    
    /// <summary>
    /// Get all walk-in guests created by a specific user
    /// </summary>
    Task<IEnumerable<GuestDto>> GetGuestsCreatedByUserAsync(string userId);
    
    /// <summary>
    /// Get all guests accessible to the current user
    /// (Walk-in guests they created + all registered users)
    /// </summary>
    Task<IEnumerable<GuestDto>> GetMyAccessibleGuestsAsync(string currentUserId);

    /// <summary>
    /// Get or create guest profile for a logged-in user
    /// Returns existing guest if found by UserId, creates new one if not
    /// </summary>
    Task<GuestDto> GetOrCreateGuestProfileAsync(string userId);
}
