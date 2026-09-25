using System.Security.Claims;
using HotelManagement.Data;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers;

/// <summary>
/// Hotel owners manage the managers and housekeepers of the hotels they own, within
/// their plan's staff limit. (SuperAdmins manage every account through /api/Users.)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin}")]
public class StaffController : ControllerBase
{
    private static readonly string[] StaffRoles = { AppRoles.Manager, AppRoles.Housekeeper };

    private readonly ApplicationDbContext _context;
    private readonly IUserService _userService;
    private readonly IEntitlementService _entitlements;

    public StaffController(ApplicationDbContext context, IUserService userService, IEntitlementService entitlements)
    {
        _context = context;
        _userService = userService;
        _entitlements = entitlements;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>
    /// Hotels whose staff the caller manages: the ones they own (all hotels for a SuperAdmin)
    /// </summary>
    private Task<List<int>> GetManagedHotelIdsAsync() => User.IsInRole(AppRoles.SuperAdmin)
        ? _context.Hotels.Select(h => h.Id).ToListAsync()
        : _context.Hotels.Where(h => h.OwnerId == CurrentUserId).Select(h => h.Id).ToListAsync();

    /// <summary>
    /// A manager or housekeeper working at one of the caller's hotels, or null
    /// </summary>
    private async Task<UserDto?> GetManagedStaffAsync(string userId)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        if (user?.HotelId == null || !user.Roles.Any(StaffRoles.Contains))
            return null;

        return (await GetManagedHotelIdsAsync()).Contains(user.HotelId.Value) ? user : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetStaff()
    {
        var staff = new List<UserDto>();
        foreach (var hotelId in await GetManagedHotelIdsAsync())
            staff.AddRange((await _userService.GetUsersByHotelAsync(hotelId)).Where(u => u.Roles.Any(StaffRoles.Contains)));

        return Ok(staff.OrderBy(u => u.HotelName).ThenBy(u => u.LastName));
    }

    [HttpPost]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffDto dto)
    {
        if (!StaffRoles.Contains(dto.Role))
            return BadRequest(new { message = "Staff can be a Manager or a Housekeeper" });

        if (!(await GetManagedHotelIdsAsync()).Contains(dto.HotelId))
            return Forbid();

        await _entitlements.EnsureCanAddStaffAsync(dto.HotelId);

        var user = await _userService.CreateUserAsync(new CreateUserDto
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Password = dto.Password,
            Role = dto.Role,
            HotelId = dto.HotelId,
            PhoneNumber = dto.PhoneNumber,
            JobTitle = dto.JobTitle ?? dto.Role
        });
        return CreatedAtAction(nameof(GetStaff), user);
    }

    [HttpPost("{userId}/deactivate")]
    public async Task<IActionResult> Deactivate(string userId)
    {
        if (await GetManagedStaffAsync(userId) == null)
            return NotFound();

        await _userService.DeactivateUserAsync(userId);
        return Ok(new { message = "Staff member deactivated" });
    }

    [HttpPost("{userId}/activate")]
    public async Task<IActionResult> Activate(string userId)
    {
        var staff = await GetManagedStaffAsync(userId);
        if (staff == null)
            return NotFound();

        // An active account counts towards the plan's staff limit
        if (!staff.IsActive)
            await _entitlements.EnsureCanAddStaffAsync(staff.HotelId!.Value);

        await _userService.ActivateUserAsync(userId);
        return Ok(new { message = "Staff member activated" });
    }

    /// <summary>
    /// Move a staff member to another of the caller's hotels
    /// </summary>
    [HttpPatch("{userId}/hotel")]
    public async Task<IActionResult> MoveToHotel(string userId, [FromBody] AssignHotelRequest request)
    {
        if (await GetManagedStaffAsync(userId) == null)
            return NotFound();

        if (request.HotelId == null || !(await GetManagedHotelIdsAsync()).Contains(request.HotelId.Value))
            return BadRequest(new { message = "Choose one of your hotels" });

        await _userService.AssignUserToHotelAsync(userId, request.HotelId);
        return Ok(new { message = "Staff member moved" });
    }
}
