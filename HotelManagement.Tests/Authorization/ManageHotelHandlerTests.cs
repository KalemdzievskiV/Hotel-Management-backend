using HotelManagement.Authorization.Handlers;
using HotelManagement.Authorization.Requirements;
using HotelManagement.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Xunit;

namespace HotelManagement.Tests.Authorization;

public class ManageHotelHandlerTests
{
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
    public async Task SuperAdmin_CanManageAnyHotel()
    {
        // Arrange
        var handler = new ManageHotelHandler();
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "other-user-id"
        };

        var user = CreateUser("super-admin-id", "SuperAdmin");
        var requirement = new ManageHotelRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.True(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Admin_CanManageOwnHotel()
    {
        // Arrange
        var handler = new ManageHotelHandler();
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "admin-user-id"
        };

        var user = CreateUser("admin-user-id", "Admin");
        var requirement = new ManageHotelRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.True(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Admin_CannotManageOtherAdminsHotel()
    {
        // Arrange
        var handler = new ManageHotelHandler();
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "other-admin-id"
        };

        var user = CreateUser("admin-user-id", "Admin");
        var requirement = new ManageHotelRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.False(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Manager_CannotManageHotels()
    {
        // Arrange
        var handler = new ManageHotelHandler();
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "manager-user-id"
        };

        var user = CreateUser("manager-user-id", "Manager");
        var requirement = new ManageHotelRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert - Manager cannot manage hotels even if they created them
        Assert.False(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Guest_CannotManageHotels()
    {
        // Arrange
        var handler = new ManageHotelHandler();
        
        var hotel = new Hotel
        {
            Id = 1,
            Name = "Test Hotel",
            OwnerId = "guest-user-id"
        };

        var user = CreateUser("guest-user-id", "Guest");
        var requirement = new ManageHotelRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, hotel);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.False(authContext.HasSucceeded);
    }

    [Fact]
    public async Task SuperAdmin_CanCreateHotels()
    {
        // Arrange
        var handler = new CreateHotelHandler();
        
        var user = CreateUser("super-admin-id", "SuperAdmin");
        var requirement = new ManageHotelRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.True(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Admin_CanCreateHotels()
    {
        // Arrange
        var handler = new CreateHotelHandler();
        
        var user = CreateUser("admin-user-id", "Admin");
        var requirement = new ManageHotelRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.True(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Manager_CannotCreateHotels()
    {
        // Arrange
        var handler = new CreateHotelHandler();
        
        var user = CreateUser("manager-user-id", "Manager");
        var requirement = new ManageHotelRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.False(authContext.HasSucceeded);
    }

    [Fact]
    public async Task Guest_CannotCreateHotels()
    {
        // Arrange
        var handler = new CreateHotelHandler();
        
        var user = CreateUser("guest-user-id", "Guest");
        var requirement = new ManageHotelRequirement();
        var authContext = new AuthorizationHandlerContext(
            new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        Assert.False(authContext.HasSucceeded);
    }
}
