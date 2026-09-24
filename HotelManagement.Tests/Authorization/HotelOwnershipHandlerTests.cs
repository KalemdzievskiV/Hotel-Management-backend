using HotelManagement.Authorization.Handlers;
using HotelManagement.Authorization.Requirements;
using HotelManagement.Data;
using HotelManagement.Models.Entities;
using HotelManagement.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Xunit;

namespace HotelManagement.Tests.Authorization;

public class HotelOwnershipHandlerTests
{
    private readonly ApplicationDbContext _context;
    private readonly HotelOwnershipHandler _handler;

    public HotelOwnershipHandlerTests()
    {
        _context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var hotelAccess = new HotelAccessService(_context, new Mock<IHttpContextAccessor>().Object);
        _handler = new HotelOwnershipHandler(hotelAccess);
    }

    private async Task<Hotel> AddHotelAsync(int id, string ownerId)
    {
        var hotel = new Hotel { Id = id, Name = $"Hotel {id}", OwnerId = ownerId, Address = "A", City = "C", Country = "X" };
        _context.Hotels.Add(hotel);
        await _context.SaveChangesAsync();
        return hotel;
    }

    private async Task AddUserAsync(string userId, int? assignedHotelId)
    {
        _context.Users.Add(new ApplicationUser { Id = userId, UserName = userId, FirstName = "F", LastName = "L", HotelId = assignedHotelId });
        await _context.SaveChangesAsync();
    }

    private static ClaimsPrincipal CreateUser(string userId, string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        }, "Test"));

    private async Task<bool> AuthorizeAsync(ClaimsPrincipal user, Hotel hotel)
    {
        var authContext = new AuthorizationHandlerContext(new[] { new HotelOwnershipRequirement() }, user, hotel);
        await _handler.HandleAsync(authContext);
        return authContext.HasSucceeded;
    }

    [Fact]
    public async Task SuperAdmin_CanAccessAnyHotel()
    {
        var hotel = await AddHotelAsync(1, "other-user-id");

        Assert.True(await AuthorizeAsync(CreateUser("super-admin-id", "SuperAdmin"), hotel));
    }

    [Fact]
    public async Task Admin_CanAccessOwnHotel()
    {
        var hotel = await AddHotelAsync(1, "admin-id");
        await AddUserAsync("admin-id", null);

        Assert.True(await AuthorizeAsync(CreateUser("admin-id", "Admin"), hotel));
    }

    [Fact]
    public async Task Admin_CannotAccessOtherAdminsHotel()
    {
        var hotel = await AddHotelAsync(1, "other-admin-id");
        await AddUserAsync("admin-id", null);

        Assert.False(await AuthorizeAsync(CreateUser("admin-id", "Admin"), hotel));
    }

    [Fact]
    public async Task Manager_CanAccessAssignedHotel()
    {
        var hotel = await AddHotelAsync(1, "owner-id");
        await AddUserAsync("manager-id", assignedHotelId: 1);

        Assert.True(await AuthorizeAsync(CreateUser("manager-id", "Manager"), hotel));
    }

    [Fact]
    public async Task Manager_CannotAccessOtherHotel()
    {
        await AddHotelAsync(1, "owner-id");
        var otherHotel = await AddHotelAsync(2, "owner-id");
        await AddUserAsync("manager-id", assignedHotelId: 1);

        Assert.False(await AuthorizeAsync(CreateUser("manager-id", "Manager"), otherHotel));
    }

    [Fact]
    public async Task Manager_OwningAHotelWithoutAssignment_CannotAccessIt()
    {
        // Ownership only grants access to Admins; Managers work at the hotel they're assigned to
        var hotel = await AddHotelAsync(1, "manager-id");
        await AddUserAsync("manager-id", null);

        Assert.False(await AuthorizeAsync(CreateUser("manager-id", "Manager"), hotel));
    }

    [Fact]
    public async Task Housekeeper_CanAccessAssignedHotel()
    {
        var hotel = await AddHotelAsync(1, "owner-id");
        await AddUserAsync("housekeeper-id", assignedHotelId: 1);

        Assert.True(await AuthorizeAsync(CreateUser("housekeeper-id", "Housekeeper"), hotel));
    }

    [Fact]
    public async Task Guest_CannotAccessAnyHotel()
    {
        var hotel = await AddHotelAsync(1, "guest-id");
        await AddUserAsync("guest-id", assignedHotelId: 1);

        Assert.False(await AuthorizeAsync(CreateUser("guest-id", "Guest"), hotel));
    }
}
