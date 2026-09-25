using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Controllers;
using HotelManagement.Models.DTOs;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Staff accounts are managed by the SuperAdmin only
/// </summary>
public class UsersIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly TestApi _api;

    public UsersIntegrationTests(CustomWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
    }

    private Task<string> SuperAdminTokenAsync() => TestAuth.GetTokenAsync(_api.Client, "SuperAdmin");

    private static CreateUserDto NewStaff(string role = "Manager", int? hotelId = null) => new()
    {
        FirstName = "New",
        LastName = "Staff",
        Email = $"staff{Guid.NewGuid():N}@test.com",
        Password = "Test123",
        Role = role,
        HotelId = hotelId
    };

    [Theory]
    [InlineData("Admin")]
    [InlineData("Manager")]
    [InlineData("Housekeeper")]
    [InlineData("Guest")]
    public async Task OnlyTheSuperAdmin_ManagesUsers(string role)
    {
        var token = await TestAuth.GetTokenAsync(_api.Client, role);

        (await _api.SendAsync(HttpMethod.Get, "/api/Users", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, "/api/Users", token, NewStaff())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StaffCannotBeAssignedToAHotelThatDoesNotExist()
    {
        var superAdminToken = await SuperAdminTokenAsync();

        (await _api.SendAsync(HttpMethod.Post, "/api/Users", superAdminToken, NewStaff(hotelId: 999_999)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var user = await _api.PostAsync<UserDto>("/api/Users", superAdminToken, NewStaff());
        (await _api.SendAsync(HttpMethod.Patch, $"/api/Users/{user.Id}/hotel", superAdminToken, new AssignHotelRequest { HotelId = 999_999 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MovingAManagerToAnotherHotel_MovesTheirAccessImmediately()
    {
        var hotelA = await _api.CreateHotelAsync();
        var hotelB = await _api.CreateHotelAsync();
        var email = $"mover{Guid.NewGuid():N}@test.com";
        var managerToken = await TestAuth.GetTokenAsync(_api.Client, "Manager", email, hotelA.HotelId);
        var managerId = await _api.GetUserIdAsync(email);

        (await _api.SendAsync(HttpMethod.Get, $"/api/Housekeeping/hotel/{hotelA.HotelId}", managerToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _api.SendAsync(HttpMethod.Patch, $"/api/Users/{managerId}/hotel", await SuperAdminTokenAsync(), new AssignHotelRequest { HotelId = hotelB.HotelId }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Same token, new hotel
        (await _api.SendAsync(HttpMethod.Get, $"/api/Housekeeping/hotel/{hotelA.HotelId}", managerToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Get, $"/api/Housekeeping/hotel/{hotelB.HotelId}", managerToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheSuperAdmin_CannotLockThemselvesOut()
    {
        var superAdminToken = await SuperAdminTokenAsync();
        var myId = await _api.GetUserIdAsync(TestAuth.SuperAdminEmail);

        (await _api.SendAsync(HttpMethod.Patch, $"/api/Users/{myId}/role", superAdminToken, new UpdateRoleRequest { Role = "Guest" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _api.SendAsync(HttpMethod.Delete, $"/api/Users/{myId}/roles/SuperAdmin", superAdminToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _api.SendAsync(HttpMethod.Post, $"/api/Users/{myId}/deactivate", superAdminToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _api.SendAsync(HttpMethod.Delete, $"/api/Users/{myId}", superAdminToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Still a working SuperAdmin
        (await _api.SendAsync(HttpMethod.Get, "/api/Users", await SuperAdminTokenAsync())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReactivatedUsers_CanSignInAgain()
    {
        var superAdminToken = await SuperAdminTokenAsync();
        var staff = NewStaff();
        var user = await _api.PostAsync<UserDto>("/api/Users", superAdminToken, staff);

        await _api.PostAsync<object>($"/api/Users/{user.Id}/deactivate", superAdminToken);
        var refused = await _api.Client.PostAsJsonAsync("/api/Auth/login", new { staff.Email, staff.Password });
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await _api.PostAsync<object>($"/api/Users/{user.Id}/activate", superAdminToken);
        (await TestAuth.LoginAsync(_api.Client, staff.Email, staff.Password)).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UnknownRoles_AreRefused()
    {
        var superAdminToken = await SuperAdminTokenAsync();

        (await _api.SendAsync(HttpMethod.Post, "/api/Users", superAdminToken, NewStaff(role: "Overlord")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var user = await _api.PostAsync<UserDto>("/api/Users", superAdminToken, NewStaff());
        (await _api.SendAsync(HttpMethod.Patch, $"/api/Users/{user.Id}/role", superAdminToken, new UpdateRoleRequest { Role = "Overlord" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
