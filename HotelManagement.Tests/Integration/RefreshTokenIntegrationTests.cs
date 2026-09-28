using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

public class RefreshTokenIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestApi _api;

    public RefreshTokenIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _api = new TestApi(_client);
    }

    private async Task<(AuthResponseDto Auth, string Email)> SignInGuestAsync()
    {
        var email = $"refresh{Guid.NewGuid():N}@test.com";
        await TestAuth.GetTokenAsync(_client, "Guest", email);
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequestDto { Email = email, Password = "Test123" });
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<AuthResponseDto>())!, email);
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/api/Auth/refresh", new RefreshTokenRequestDto { RefreshToken = refreshToken });

    private async Task<HttpStatusCode> GetStatusAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await _client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task Login_ReturnsRefreshToken_ThatGivesANewWorkingSession()
    {
        var (auth, email) = await SignInGuestAsync();
        auth.RefreshToken.Should().NotBeNullOrEmpty();
        auth.RefreshTokenExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(29));

        var response = await RefreshAsync(auth.RefreshToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;

        refreshed.Email.Should().Be(email);
        refreshed.Roles.Should().Contain("Guest");
        refreshed.RefreshToken.Should().NotBe(auth.RefreshToken);
        (await GetStatusAsync("/api/Reservations", refreshed.Token)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_AlsoReturnsRefreshToken()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/register", new RegisterRequestDto
        {
            FirstName = "New",
            LastName = "Guest",
            Email = $"register{Guid.NewGuid():N}@test.com",
            Password = "Test123",
            Role = "Guest"
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;

        (await RefreshAsync(auth.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReusingARotatedToken_EndsAllSessionsOfThatUser()
    {
        var (auth, _) = await SignInGuestAsync();
        var first = (await (await RefreshAsync(auth.RefreshToken)).Content.ReadFromJsonAsync<AuthResponseDto>())!;

        // The original token was already used: this looks like a stolen copy
        (await RefreshAsync(auth.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // ...so the legitimate newer token is revoked too
        (await RefreshAsync(first.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var (auth, _) = await SignInGuestAsync();

        var logout = await _client.PostAsJsonAsync("/api/Auth/logout", new RefreshTokenRequestDto { RefreshToken = auth.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RefreshAsync(auth.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithUnknownToken_IsHarmless()
    {
        var logout = await _client.PostAsJsonAsync("/api/Auth/logout", new RefreshTokenRequestDto { RefreshToken = "not-a-token" });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnknownToken_IsRejected()
    {
        (await RefreshAsync("not-a-token")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivatedUser_CannotRefresh()
    {
        var (auth, email) = await SignInGuestAsync();
        var userId = await _api.GetUserIdAsync(email);
        var superAdminToken = await TestAuth.GetTokenAsync(_client, "SuperAdmin");
        (await _api.SendAsync(HttpMethod.Post, $"/api/Users/{userId}/deactivate", superAdminToken)).EnsureSuccessStatusCode();

        (await RefreshAsync(auth.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RoleChange_EndsRefreshTokens()
    {
        var (auth, email) = await SignInGuestAsync();
        var userId = await _api.GetUserIdAsync(email);
        var superAdminToken = await TestAuth.GetTokenAsync(_client, "SuperAdmin");
        (await _api.SendAsync(HttpMethod.Patch, $"/api/Users/{userId}/role", superAdminToken, new { Role = "Housekeeper" }))
            .EnsureSuccessStatusCode();

        (await RefreshAsync(auth.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
