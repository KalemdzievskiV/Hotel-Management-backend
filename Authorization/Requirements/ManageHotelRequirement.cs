using Microsoft.AspNetCore.Authorization;

namespace HotelManagement.Authorization.Requirements;

/// <summary>
/// Requirement for creating, editing, or deleting hotels
/// SuperAdmin: Can manage all hotels
/// Admin: Can manage their own hotels
/// Manager: Cannot manage hotels (read-only access)
/// </summary>
public class ManageHotelRequirement : IAuthorizationRequirement
{
    public ManageHotelRequirement()
    {
    }
}
