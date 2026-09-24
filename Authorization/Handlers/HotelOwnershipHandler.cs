using HotelManagement.Authorization.Requirements;
using HotelManagement.Models.Entities;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace HotelManagement.Authorization.Handlers;

/// <summary>
/// Grants access to a hotel's operational data when the user can access that hotel
/// (see <see cref="IHotelAccessService"/> for the rules).
/// </summary>
public class HotelOwnershipHandler : AuthorizationHandler<HotelOwnershipRequirement, Hotel>
{
    private readonly IHotelAccessService _hotelAccess;

    public HotelOwnershipHandler(IHotelAccessService hotelAccess)
    {
        _hotelAccess = hotelAccess;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HotelOwnershipRequirement requirement,
        Hotel resource)
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync(context.User);
        if (hotelIds.Contains(resource.Id))
            context.Succeed(requirement);
    }
}

/// <summary>
/// Same as <see cref="HotelOwnershipHandler"/> but takes the hotel ID as the resource.
/// </summary>
public class HotelOwnershipByIdHandler : AuthorizationHandler<HotelOwnershipRequirement, int>
{
    private readonly IHotelAccessService _hotelAccess;

    public HotelOwnershipByIdHandler(IHotelAccessService hotelAccess)
    {
        _hotelAccess = hotelAccess;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HotelOwnershipRequirement requirement,
        int hotelId)
    {
        var hotelIds = await _hotelAccess.GetAccessibleHotelIdsAsync(context.User);
        if (hotelIds.Contains(hotelId))
            context.Succeed(requirement);
    }
}
