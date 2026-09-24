using System.Security.Claims;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// Single source of truth for which hotels the current user may work with.
/// SuperAdmin: every hotel. Admin: hotels they own plus the hotel they're assigned to.
/// Manager/Housekeeper: the hotel they're assigned to (ApplicationUser.HotelId).
/// Guests have no staff access to any hotel.
/// </summary>
public interface IHotelAccessService
{
    /// <summary>Hotels accessible to the current request's user (cached per request).</summary>
    Task<IReadOnlyList<int>> GetAccessibleHotelIdsAsync();

    /// <summary>Hotels accessible to an explicit principal, e.g. inside authorization handlers.</summary>
    Task<IReadOnlyList<int>> GetAccessibleHotelIdsAsync(ClaimsPrincipal user);

    Task<bool> CanAccessHotelAsync(int hotelId);

    /// <summary>
    /// True when the current user can see the guest: walk-in guests of their hotels
    /// and guests with a reservation at one of their hotels.
    /// </summary>
    Task<bool> CanAccessGuestAsync(int guestId);

    /// <summary>Hotel of the given room, or null if the room doesn't exist.</summary>
    Task<int?> GetRoomHotelIdAsync(int roomId);
}
