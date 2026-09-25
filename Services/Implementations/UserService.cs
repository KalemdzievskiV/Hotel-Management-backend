using HotelManagement.Models.Constants;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

public class UserService : Services.Interfaces.IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly Interfaces.IEntitlementService _entitlements;

    public UserService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context, Interfaces.IEntitlementService entitlements)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _entitlements = entitlements;
    }

    /// <summary>
    /// Hotel owners (Admins) need a subscription; a new owner starts with a trial
    /// </summary>
    private async Task EnsureOwnerSubscriptionAsync(string userId, string role)
    {
        if (role == AppRoles.Admin)
            await _entitlements.EnsureSubscriptionAsync(userId);
    }

    /// <summary>
    /// Staff are assigned by hotel id; an unknown id would otherwise fail on the database's foreign key
    /// </summary>
    private async Task EnsureHotelExistsAsync(int? hotelId)
    {
        if (hotelId.HasValue && !await _context.Hotels.AnyAsync(h => h.Id == hotelId.Value))
            throw new BusinessRuleException($"Hotel {hotelId} does not exist");
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto createDto)
    {
        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(createDto.Email);
        if (existingUser != null)
            throw new BusinessRuleException($"A user with email '{createDto.Email}' already exists");

        // Ensure role exists
        if (!await _roleManager.RoleExistsAsync(createDto.Role))
            throw new BusinessRuleException($"Role '{createDto.Role}' does not exist");

        await EnsureHotelExistsAsync(createDto.HotelId);

        // Create new user
        var user = new ApplicationUser
        {
            UserName = createDto.Email,
            Email = createDto.Email,
            FirstName = createDto.FirstName,
            LastName = createDto.LastName,
            PhoneNumber = createDto.PhoneNumber,
            DateOfBirth = createDto.DateOfBirth,
            Gender = createDto.Gender,
            Address = createDto.Address,
            City = createDto.City,
            State = createDto.State,
            Country = createDto.Country,
            PostalCode = createDto.PostalCode,
            JobTitle = createDto.JobTitle,
            Department = createDto.Department,
            HotelId = createDto.HotelId,
            EmergencyContactName = createDto.EmergencyContactName,
            EmergencyContactPhone = createDto.EmergencyContactPhone,
            EmergencyContactRelationship = createDto.EmergencyContactRelationship,
            PreferredLanguage = createDto.PreferredLanguage,
            TimeZone = createDto.TimeZone,
            EmailNotifications = createDto.EmailNotifications,
            SmsNotifications = createDto.SmsNotifications,
            IsActive = createDto.IsActive,
            Notes = createDto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, createDto.Password);
        if (!result.Succeeded)
            throw new BusinessRuleException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        // Assign role
        var roleResult = await _userManager.AddToRoleAsync(user, createDto.Role);
        if (!roleResult.Succeeded)
        {
            // Rollback user creation if role assignment fails
            await _userManager.DeleteAsync(user);
            throw new BusinessRuleException($"Failed to assign role: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
        }

        await EnsureOwnerSubscriptionAsync(user.Id, createDto.Role);

        return await GetUserByIdAsync(user.Id) ?? throw new InvalidOperationException("User not found after creation");
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync(int? skip = null, int? take = null)
    {
        var query = _userManager.Users
            .Include(u => u.Hotel)
            .AsQueryable();

        if (skip.HasValue)
            query = query.Skip(skip.Value);

        if (take.HasValue)
            query = query.Take(take.Value);

        var users = await query.ToListAsync();
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var userDto = await MapToUserDto(user);
            userDtos.Add(userDto);
        }

        return userDtos;
    }

    public async Task<UserDto?> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.Users
            .Include(u => u.Hotel)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return null;

        return await MapToUserDto(user);
    }

    public async Task<IEnumerable<UserDto>> GetUsersByRoleAsync(string role)
    {
        var usersInRole = await _userManager.GetUsersInRoleAsync(role);
        var userDtos = new List<UserDto>();

        foreach (var user in usersInRole)
        {
            // Reload with Hotel navigation property
            var userWithHotel = await _userManager.Users
                .Include(u => u.Hotel)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (userWithHotel != null)
            {
                var userDto = await MapToUserDto(userWithHotel);
                userDtos.Add(userDto);
            }
        }

        return userDtos;
    }

    public async Task<IEnumerable<UserDto>> GetUsersByHotelAsync(int hotelId)
    {
        var users = await _userManager.Users
            .Include(u => u.Hotel)
            .Where(u => u.HotelId == hotelId)
            .ToListAsync();

        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var userDto = await MapToUserDto(user);
            userDtos.Add(userDto);
        }

        return userDtos;
    }

    public async Task<IEnumerable<UserDto>> SearchUsersAsync(string searchTerm)
    {
        var users = await _userManager.Users
            .Include(u => u.Hotel)
            .Where(u => 
                u.FirstName.Contains(searchTerm) ||
                u.LastName.Contains(searchTerm) ||
                u.Email!.Contains(searchTerm) ||
                u.PhoneNumber!.Contains(searchTerm))
            .ToListAsync();

        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var userDto = await MapToUserDto(user);
            userDtos.Add(userDto);
        }

        return userDtos;
    }

    public async Task<UserDto> UpdateUserAsync(string userId, UpdateUserDto updateDto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {userId} not found");

        await EnsureHotelExistsAsync(updateDto.HotelId);

        // Update properties
        user.FirstName = updateDto.FirstName;
        user.LastName = updateDto.LastName;
        user.PhoneNumber = updateDto.PhoneNumber;
        user.ProfilePictureUrl = updateDto.ProfilePictureUrl;
        user.DateOfBirth = updateDto.DateOfBirth;
        user.Gender = updateDto.Gender;
        user.Address = updateDto.Address;
        user.City = updateDto.City;
        user.State = updateDto.State;
        user.Country = updateDto.Country;
        user.PostalCode = updateDto.PostalCode;
        user.JobTitle = updateDto.JobTitle;
        user.Department = updateDto.Department;
        user.HotelId = updateDto.HotelId;
        user.EmergencyContactName = updateDto.EmergencyContactName;
        user.EmergencyContactPhone = updateDto.EmergencyContactPhone;
        user.EmergencyContactRelationship = updateDto.EmergencyContactRelationship;
        user.PreferredLanguage = updateDto.PreferredLanguage;
        user.TimeZone = updateDto.TimeZone;
        user.EmailNotifications = updateDto.EmailNotifications;
        user.SmsNotifications = updateDto.SmsNotifications;
        user.Notes = updateDto.Notes;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new BusinessRuleException($"Failed to update user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return await GetUserByIdAsync(userId) ?? throw new InvalidOperationException("User not found after update");
    }

    public async Task ActivateUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {userId} not found");

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new BusinessRuleException($"Failed to activate user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }

    public async Task DeactivateUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {userId} not found");

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateSecurityStampAsync(user); // ends existing sessions

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new BusinessRuleException($"Failed to deactivate user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }

    public async Task DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {userId} not found");

        try
        {
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                throw new BusinessRuleException($"Failed to delete user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        catch (DbUpdateException ex)
        {
            // Check if it's a foreign key constraint violation
            if (ex.InnerException?.Message.Contains("FK_") == true || 
                ex.InnerException?.Message.Contains("REFERENCE constraint") == true)
            {
                throw new BusinessRuleException(
                    "Cannot delete this user because they own hotels or have related data. " +
                    "Please reassign or remove their hotels before deleting the user.");
            }
            
            throw; // Re-throw if it's a different type of error
        }
    }

    public async Task AssignUserToHotelAsync(string userId, int? hotelId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {userId} not found");

        await EnsureHotelExistsAsync(hotelId);

        user.HotelId = hotelId;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new BusinessRuleException($"Failed to assign user to hotel: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }

    public async Task UpdateUserRoleAsync(string userId, string newRole)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {userId} not found");

        // Ensure role exists
        if (!await _roleManager.RoleExistsAsync(newRole))
            throw new BusinessRuleException($"Role '{newRole}' does not exist");

        // Remove all existing roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
            throw new BusinessRuleException($"Failed to remove existing roles: {string.Join(", ", removeResult.Errors.Select(e => e.Description))}");

        // Add new role
        var addResult = await _userManager.AddToRoleAsync(user, newRole);
        if (!addResult.Succeeded)
            throw new BusinessRuleException($"Failed to add new role: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");

        await EnsureOwnerSubscriptionAsync(user.Id, newRole);

        // Tokens carry roles, so end existing sessions to apply the change immediately
        await _userManager.UpdateSecurityStampAsync(user);
    }

    public async Task AddRoleToUserAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {userId} not found");

        // Ensure role exists
        if (!await _roleManager.RoleExistsAsync(role))
            throw new BusinessRuleException($"Role '{role}' does not exist");

        var result = await _userManager.AddToRoleAsync(user, role);
        if (!result.Succeeded)
            throw new BusinessRuleException($"Failed to add role: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await EnsureOwnerSubscriptionAsync(user.Id, role);

        // Tokens carry roles, so end existing sessions to apply the change immediately
        await _userManager.UpdateSecurityStampAsync(user);
    }

    public async Task RemoveRoleFromUserAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {userId} not found");

        var result = await _userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
            throw new BusinessRuleException($"Failed to remove role: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        // Tokens carry roles, so end existing sessions to apply the change immediately
        await _userManager.UpdateSecurityStampAsync(user);
    }

    public async Task UpdateLastLoginAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new KeyNotFoundException($"User with ID {userId} not found");

        user.LastLoginDate = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new BusinessRuleException($"Failed to update last login: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }

    public async Task<int> GetTotalUserCountAsync()
    {
        return await _userManager.Users.CountAsync();
    }

    public async Task<Dictionary<string, int>> GetUserCountByRoleAsync()
    {
        var roles = await _roleManager.Roles.ToListAsync();
        var result = new Dictionary<string, int>();

        foreach (var role in roles)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
            result[role.Name!] = usersInRole.Count;
        }

        return result;
    }

    // Helper method to map ApplicationUser to UserDto
    private async Task<UserDto> MapToUserDto(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            ProfilePictureUrl = user.ProfilePictureUrl,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender,
            Address = user.Address,
            City = user.City,
            State = user.State,
            Country = user.Country,
            PostalCode = user.PostalCode,
            JobTitle = user.JobTitle,
            Department = user.Department,
            HotelId = user.HotelId,
            HotelName = user.Hotel?.Name,
            EmergencyContactName = user.EmergencyContactName,
            EmergencyContactPhone = user.EmergencyContactPhone,
            EmergencyContactRelationship = user.EmergencyContactRelationship,
            PreferredLanguage = user.PreferredLanguage,
            TimeZone = user.TimeZone,
            EmailNotifications = user.EmailNotifications,
            SmsNotifications = user.SmsNotifications,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginDate = user.LastLoginDate,
            Roles = roles.ToList(),
            Notes = user.Notes
        };
    }
}
