using System.Security.Claims;
using HotelManagement.Authorization.Requirements;
using HotelManagement.Data;
using HotelManagement.Models.Constants;
using HotelManagement.Models.Entities;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Authorization.Handlers;

/// <summary>
/// Shared rule: guests may access reservations made for their own guest profile;
/// staff may access reservations at hotels they can access.
/// </summary>
internal static class ReservationAccessRules
{
    public static async Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        int reservationHotelId,
        string? reservationGuestUserId,
        IHotelAccessService hotelAccess)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return false;

        if (user.IsInRole(AppRoles.Guest))
            return reservationGuestUserId == userId;

        var hotelIds = await hotelAccess.GetAccessibleHotelIdsAsync(user);
        return hotelIds.Contains(reservationHotelId);
    }
}

public class ReservationAccessHandler : AuthorizationHandler<ReservationAccessRequirement, Reservation>
{
    private readonly ApplicationDbContext _context;
    private readonly IHotelAccessService _hotelAccess;

    public ReservationAccessHandler(ApplicationDbContext context, IHotelAccessService hotelAccess)
    {
        _context = context;
        _hotelAccess = hotelAccess;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ReservationAccessRequirement requirement,
        Reservation resource)
    {
        var guestUserId = resource.Guest?.UserId ?? await _context.Guests
            .Where(g => g.Id == resource.GuestId)
            .Select(g => g.UserId)
            .FirstOrDefaultAsync();

        if (await ReservationAccessRules.CanAccessAsync(context.User, resource.HotelId, guestUserId, _hotelAccess))
            context.Succeed(requirement);
    }
}

public class ReservationAccessByIdHandler : AuthorizationHandler<ReservationAccessRequirement, int>
{
    private readonly ApplicationDbContext _context;
    private readonly IHotelAccessService _hotelAccess;

    public ReservationAccessByIdHandler(ApplicationDbContext context, IHotelAccessService hotelAccess)
    {
        _context = context;
        _hotelAccess = hotelAccess;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ReservationAccessRequirement requirement,
        int reservationId)
    {
        var reservation = await _context.Reservations
            .Where(r => r.Id == reservationId)
            .Select(r => new { r.HotelId, GuestUserId = r.Guest.UserId })
            .FirstOrDefaultAsync();

        if (reservation == null)
            return;

        if (await ReservationAccessRules.CanAccessAsync(context.User, reservation.HotelId, reservation.GuestUserId, _hotelAccess))
            context.Succeed(requirement);
    }
}
