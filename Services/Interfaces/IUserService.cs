using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// Service interface for User management operations
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Create new user (SuperAdmin only)
    /// </summary>
    Task<UserDto> CreateUserAsync(CreateUserDto createDto);
    
    /// <summary>
    /// Get all users with optional pagination
    /// </summary>
    Task<IEnumerable<UserDto>> GetAllUsersAsync(int? skip = null, int? take = null);
    
    /// <summary>
    /// Get user by ID
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(string userId);
    
    /// <summary>
    /// Get users by role
    /// </summary>
    Task<IEnumerable<UserDto>> GetUsersByRoleAsync(string role);
    
    /// <summary>
    /// Get users assigned to a specific hotel
    /// </summary>
    Task<IEnumerable<UserDto>> GetUsersByHotelAsync(int hotelId);
    
    /// <summary>
    /// Search users by name or email
    /// </summary>
    Task<IEnumerable<UserDto>> SearchUsersAsync(string searchTerm);
    
    /// <summary>
    /// Update user profile
    /// </summary>
    Task<UserDto> UpdateUserAsync(string userId, UpdateUserDto updateDto);
    
    /// <summary>
    /// Activate user account
    /// </summary>
    Task ActivateUserAsync(string userId);
    
    /// <summary>
    /// Deactivate user account
    /// </summary>
    Task DeactivateUserAsync(string userId);
    
    /// <summary>
    /// Delete user (hard delete)
    /// </summary>
    Task DeleteUserAsync(string userId);
    
    /// <summary>
    /// Assign user to hotel (for staff members)
    /// </summary>
    Task AssignUserToHotelAsync(string userId, int? hotelId);
    
    /// <summary>
    /// Update user role
    /// </summary>
    Task UpdateUserRoleAsync(string userId, string newRole);
    
    /// <summary>
    /// Add role to user
    /// </summary>
    Task AddRoleToUserAsync(string userId, string role);
    
    /// <summary>
    /// Remove role from user
    /// </summary>
    Task RemoveRoleFromUserAsync(string userId, string role);
    
    /// <summary>
    /// Update last login date
    /// </summary>
    Task UpdateLastLoginAsync(string userId);
    
    /// <summary>
    /// Get total user count
    /// </summary>
    Task<int> GetTotalUserCountAsync();
    
    /// <summary>
    /// Get count of users by role
    /// </summary>
    Task<Dictionary<string, int>> GetUserCountByRoleAsync();
}
