using Microsoft.AspNetCore.Authorization;

namespace HotelManagement.Authorization.Requirements;

/// <summary>
/// Requirement for accessing reservations
/// SuperAdmin: All reservations
/// Admin/Manager: Reservations in their hotels
/// Guest: Only their own reservations
/// </summary>
public class ReservationAccessRequirement : IAuthorizationRequirement
{
    public ReservationAccessRequirement()
    {
    }
}
