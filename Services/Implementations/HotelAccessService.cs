using System.Security.Claims;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Queries;
using HotelManagement.Models.Constants;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

public class HotelAccessService : IHotelAccessService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Scoped per request, so the lookup runs at most once per request
    private IReadOnlyList<int>? _accessibleHotelIds;

    public HotelAccessService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public async Task<IReadOnlyList<int>> GetAccessibleHotelIdsAsync()
    {
        var user = User;
        if (user == null)
            return Array.Empty<int>();

        return _accessibleHotelIds ??= await GetAccessibleHotelIdsAsync(user);
    }

    public async Task<IReadOnlyList<int>> GetAccessibleHotelIdsAsync(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Array.Empty<int>();

        if (user.IsInRole(AppRoles.SuperAdmin))
            return await _context.Hotels.Select(h => h.Id).ToListAsync();

        if (!user.IsInRole(AppRoles.Admin) && !user.IsInRole(AppRoles.Manager) && !user.IsInRole(AppRoles.Housekeeper))
            return Array.Empty<int>();

        // Read the assignment from the database rather than the token so reassignments apply immediately
        var assignedHotelId = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.HotelId)
            .FirstOrDefaultAsync();

        var ids = new HashSet<int>();
        if (assignedHotelId.HasValue)
            ids.Add(assignedHotelId.Value);

        if (user.IsInRole(AppRoles.Admin))
            ids.UnionWith(await _context.Hotels.Where(h => h.OwnerId == userId).Select(h => h.Id).ToListAsync());

        return ids.ToList();
    }

    public async Task<bool> CanAccessHotelAsync(int hotelId)
    {
        var ids = await GetAccessibleHotelIdsAsync();
        return ids.Contains(hotelId);
    }

    public async Task<bool> CanAccessGuestAsync(int guestId)
    {
        var ids = await GetAccessibleHotelIdsAsync();
        if (ids.Count == 0)
            return false;

        return await _context.Guests
            .Where(GuestQueries.VisibleToHotels(ids))
            .AnyAsync(g => g.Id == guestId);
    }

    public async Task<int?> GetRoomHotelIdAsync(int roomId)
    {
        return await _context.Rooms
            .Where(r => r.Id == roomId)
            .Select(r => (int?)r.HotelId)
            .FirstOrDefaultAsync();
    }
}
