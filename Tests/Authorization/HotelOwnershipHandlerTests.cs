using HotelManagement.Authorization.Handlers;
using HotelManagement.Authorization.Requirements;
using HotelManagement.Data;
using HotelManagement.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace HotelManagement.Tests.Authorization;

public class HotelOwnershipHandlerTests
{
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new ApplicationDbContext(options);
    }

    private ClaimsPrincipal CreateUser(string userId, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task SuperAdmin_CanAccessAnyHotel()
    {
        // Arrange
        var context = CreateContext();
        var handler = new HotelOwnershipHandler(context);
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "other-user-id"
        };

        var user = CreateUser("super-admin-id", "SuperAdmin");
        var requirement = new HotelOwnershipRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.True(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Admin_CanAccessOwnHotel()
    {
        // Arrange
        var context = CreateContext();
        var handler = new HotelOwnershipHandler(context);
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "admin-user-id"
        };

        var user = CreateUser("admin-user-id", "Admin");
        var requirement = new HotelOwnershipRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.True(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Admin_CannotAccessOtherAdminsHotel()
    {
        // Arrange
        var context = CreateContext();
        var handler = new HotelOwnershipHandler(context);
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "other-admin-id"
        };

        var user = CreateUser("admin-user-id", "Admin");
        var requirement = new HotelOwnershipRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.False(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Manager_CanAccessOwnHotel()
    {
        // Arrange
        var context = CreateContext();
        var handler = new HotelOwnershipHandler(context);
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "manager-user-id"
        };

        var user = CreateUser("manager-user-id", "Manager");
        var requirement = new HotelOwnershipRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.True(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Manager_CannotAccessOtherHotel()
    {
        // Arrange
        var context = CreateContext();
        var handler = new HotelOwnershipHandler(context);
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "other-user-id"
        };

        var user = CreateUser("manager-user-id", "Manager");
        var requirement = new HotelOwnershipRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.False(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Guest_CannotAccessAnyHotel()
    {
        // Arrange
        var context = CreateContext();
        var handler = new HotelOwnershipHandler(context);
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "admin-user-id"
        };

        var user = CreateUser("guest-user-id", "Guest");
        var requirement = new HotelOwnershipRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.False(authContext.HasSucceeded);
    }
}
