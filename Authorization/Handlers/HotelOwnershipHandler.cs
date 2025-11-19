using HotelManagement.Authorization.Requirements;
using HotelManagement.Data;
using HotelManagement.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotelManagement.Authorization.Handlers;

/// <summary>
/// Handler for HotelOwnershipRequirement
/// Checks if user has access to view/use a hotel's resources
/// </summary>
public class HotelOwnershipHandler : AuthorizationHandler<HotelOwnershipRequirement, Hotel>
{
    private readonly ApplicationDbContext _context;

    public HotelOwnershipHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HotelOwnershipRequirement requirement,
        Hotel resource)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // SuperAdmin has access to all hotels
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        // Admin/Manager: Check if they own the hotel
        if (context.User.IsInRole("Admin") || context.User.IsInRole("Manager"))
        {
            if (resource.OwnerId == userId)
            {
                context.Succeed(requirement);
                return;
            }
        }

        // If we reach here, user doesn't have access
        return;
    }
}

/// <summary>
/// Handler for checking hotel ownership by hotel ID
/// Used when we only have the hotel ID, not the full entity
/// </summary>
public class HotelOwnershipByIdHandler : AuthorizationHandler<HotelOwnershipRequirement, int>
{
    private readonly ApplicationDbContext _context;

    public HotelOwnershipByIdHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HotelOwnershipRequirement requirement,
        int hotelId)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // SuperAdmin has access to all hotels
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        // Admin/Manager: Check if they own the hotel
        if (context.User.IsInRole("Admin") || context.User.IsInRole("Manager"))
        {
            var hotel = await _context.Hotels
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == hotelId);

            if (hotel != null && hotel.OwnerId == userId)
            {
                context.Succeed(requirement);
                return;
            }
        }

        return;
    }
}
