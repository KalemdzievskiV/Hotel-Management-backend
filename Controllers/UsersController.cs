using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers;

/// <summary>
/// Controller for user management (SuperAdmin only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Create new user (SuperAdmin only)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto createDto)
    {
        var user = await _userService.CreateUserAsync(createDto);
        return CreatedAtAction(nameof(GetUserById), new { userId = user.Id }, user);
    }

    /// <summary>
    /// Get all users with optional pagination
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllUsers([FromQuery] int? skip, [FromQuery] int? take)
    {
        var users = await _userService.GetAllUsersAsync(skip, take);
        return Ok(users);
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserById(string userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null)
            return NotFound(new { message = $"User with ID {userId} not found" });

        return Ok(user);
    }

    /// <summary>
    /// Get users by role
    /// </summary>
    [HttpGet("role/{role}")]
    public async Task<IActionResult> GetUsersByRole(string role)
    {
        var users = await _userService.GetUsersByRoleAsync(role);
        return Ok(users);
    }

    /// <summary>
    /// Get users assigned to a specific hotel
    /// </summary>
    [HttpGet("hotel/{hotelId:int}")]
    public async Task<IActionResult> GetUsersByHotel(int hotelId)
    {
        var users = await _userService.GetUsersByHotelAsync(hotelId);
        return Ok(users);
    }

    /// <summary>
    /// Search users by name or email
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return BadRequest(new { message = "Search term cannot be empty" });

        var users = await _userService.SearchUsersAsync(searchTerm);
        return Ok(users);
    }

    /// <summary>
    /// Update user profile
    /// </summary>
    [HttpPut("{userId}")]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserDto updateDto)
    {
        var updatedUser = await _userService.UpdateUserAsync(userId, updateDto);
        return Ok(updatedUser);
    }

    /// <summary>
    /// Activate user account
    /// </summary>
    [HttpPost("{userId}/activate")]
    public async Task<IActionResult> ActivateUser(string userId)
    {
        await _userService.ActivateUserAsync(userId);
        return Ok(new { message = "User activated successfully" });
    }

    /// <summary>
    /// Deactivate user account
    /// </summary>
    [HttpPost("{userId}/deactivate")]
    public async Task<IActionResult> DeactivateUser(string userId)
    {
        await _userService.DeactivateUserAsync(userId);
        return Ok(new { message = "User deactivated successfully" });
    }

    /// <summary>
    /// Delete user (hard delete)
    /// </summary>
    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        await _userService.DeleteUserAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// Assign user to hotel
    /// </summary>
    [HttpPatch("{userId}/hotel")]
    public async Task<IActionResult> AssignUserToHotel(string userId, [FromBody] AssignHotelRequest request)
    {
        await _userService.AssignUserToHotelAsync(userId, request.HotelId);
        return Ok(new { message = $"User assigned to hotel {request.HotelId ?? 0} successfully" });
    }

    /// <summary>
    /// Update user role (replaces all existing roles with new role)
    /// </summary>
    [HttpPatch("{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(string userId, [FromBody] UpdateRoleRequest request)
    {
        await _userService.UpdateUserRoleAsync(userId, request.Role);
        return Ok(new { message = $"User role updated to {request.Role} successfully" });
    }

    /// <summary>
    /// Add role to user (keeps existing roles)
    /// </summary>
    [HttpPost("{userId}/roles")]
    public async Task<IActionResult> AddRoleToUser(string userId, [FromBody] UpdateRoleRequest request)
    {
        await _userService.AddRoleToUserAsync(userId, request.Role);
        return Ok(new { message = $"Role {request.Role} added to user successfully" });
    }

    /// <summary>
    /// Remove role from user
    /// </summary>
    [HttpDelete("{userId}/roles/{role}")]
    public async Task<IActionResult> RemoveRoleFromUser(string userId, string role)
    {
        await _userService.RemoveRoleFromUserAsync(userId, role);
        return Ok(new { message = $"Role {role} removed from user successfully" });
    }

    /// <summary>
    /// Get total user count
    /// </summary>
    [HttpGet("stats/count")]
    public async Task<IActionResult> GetTotalUserCount()
    {
        var count = await _userService.GetTotalUserCountAsync();
        return Ok(new { totalUsers = count });
    }

    /// <summary>
    /// Get user count by role (statistics)
    /// </summary>
    [HttpGet("stats/by-role")]
    public async Task<IActionResult> GetUserCountByRole()
    {
        var stats = await _userService.GetUserCountByRoleAsync();
        return Ok(stats);
    }
}

/// <summary>
/// Request DTO for assigning hotel to user
/// </summary>
public class AssignHotelRequest
{
    public int? HotelId { get; set; }
}

/// <summary>
/// Request DTO for updating user role
/// </summary>
public class UpdateRoleRequest
{
    public string Role { get; set; } = string.Empty;
}
