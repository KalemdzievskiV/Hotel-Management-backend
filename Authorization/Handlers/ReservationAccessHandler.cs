using HotelManagement.Authorization.Requirements;
using HotelManagement.Data;
using HotelManagement.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotelManagement.Authorization.Handlers;

/// <summary>
/// Handler for ReservationAccessRequirement
/// Controls access to reservations based on hotel ownership or guest ownership
/// </summary>
public class ReservationAccessHandler : AuthorizationHandler<ReservationAccessRequirement, Reservation>
{
    private readonly ApplicationDbContext _context;

    public ReservationAccessHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ReservationAccessRequirement requirement,
        Reservation resource)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // SuperAdmin has access to all reservations
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        // Guest: Can only access their own reservations
        if (context.User.IsInRole("Guest"))
        {
            var guest = await _context.Guests
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.UserId == userId);

            if (guest != null && resource.GuestId == guest.Id)
            {
                context.Succeed(requirement);
            }
            return;
        }

        // Admin/Manager: Can access reservations in their hotels
        if (context.User.IsInRole("Admin") || context.User.IsInRole("Manager"))
        {
            // Get the hotel for this reservation
            var room = await _context.Rooms
                .AsNoTracking()
                .Include(r => r.Hotel)
                .FirstOrDefaultAsync(r => r.Id == resource.RoomId);

            if (room?.Hotel != null && room.Hotel.OwnerId == userId)
            {
                context.Succeed(requirement);
            }
        }

        return;
    }
}

/// <summary>
/// Handler for reservation access by reservation ID
/// </summary>
public class ReservationAccessByIdHandler : AuthorizationHandler<ReservationAccessRequirement, int>
{
    private readonly ApplicationDbContext _context;

    public ReservationAccessByIdHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ReservationAccessRequirement requirement,
        int reservationId)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // SuperAdmin has access to all reservations
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        var reservation = await _context.Reservations
            .AsNoTracking()
            .Include(r => r.Room)
                .ThenInclude(room => room.Hotel)
            .Include(r => r.Guest)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            return;
        }

        // Guest: Can only access their own reservations
        if (context.User.IsInRole("Guest"))
        {
            if (reservation.Guest?.UserId == userId)
            {
                context.Succeed(requirement);
            }
            return;
        }

        // Admin/Manager: Can access reservations in their hotels
        if (context.User.IsInRole("Admin") || context.User.IsInRole("Manager"))
        {
            if (reservation.Room?.Hotel?.OwnerId == userId)
            {
                context.Succeed(requirement);
            }
        }

        return;
    }
}
