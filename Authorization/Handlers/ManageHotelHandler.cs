using HotelManagement.Authorization.Requirements;
using HotelManagement.Data;
using HotelManagement.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotelManagement.Authorization.Handlers;

/// <summary>
/// Handler for ManageHotelRequirement
/// Determines if user can create, edit, or delete hotels
/// SuperAdmin: Yes
/// Admin: Only their own hotels
/// Manager: No (read-only)
/// </summary>
public class ManageHotelHandler : AuthorizationHandler<ManageHotelRequirement, Hotel>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ManageHotelRequirement requirement,
        Hotel resource)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Task.CompletedTask;
        }

        // SuperAdmin can manage all hotels
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Admin can manage their own hotels
        if (context.User.IsInRole("Admin"))
        {
            if (resource.OwnerId == userId)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        // Manager cannot manage hotels (Fail implicitly)
        // Guest cannot manage hotels (Fail implicitly)

        return Task.CompletedTask;
    }
}

/// <summary>
/// Handler for creating new hotels (no resource yet)
/// </summary>
public class CreateHotelHandler : AuthorizationHandler<ManageHotelRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ManageHotelRequirement requirement)
    {
        // SuperAdmin and Admin can create hotels
        if (context.User.IsInRole("SuperAdmin") || context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
        }

        // Manager and Guest cannot create hotels
        return Task.CompletedTask;
    }
}
