using Microsoft.AspNetCore.Authorization;

namespace HotelManagement.Authorization.Requirements;

/// <summary>
/// Requirement for checking if a user has access to a hotel
/// SuperAdmin: All hotels
/// Admin: Only hotels they own (CreatedBy = UserId)
/// Manager: Hotels they manage (similar access to Admin for now)
/// </summary>
public class HotelOwnershipRequirement : IAuthorizationRequirement
{
    public HotelOwnershipRequirement()
    {
    }
}
