using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

public class AuthSecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthSecurityIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<HttpStatusCode> GetStatusAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await _client.SendAsync(request)).StatusCode;
    }

    private async Task<HttpStatusCode> SendAsSuperAdminAsync(HttpMethod method, string url, object? body = null)
    {
        var superAdminToken = await TestAuth.GetTokenAsync(_client, "SuperAdmin");
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
        if (body != null)
            request.Content = JsonContent.Create(body);
        return (await _client.SendAsync(request)).StatusCode;
    }

    private async Task<(string Token, string UserId)> CreateManagerAsync()
    {
        var email = $"session{Guid.NewGuid():N}@test.com";
        var token = await TestAuth.GetTokenAsync(_client, "Manager", email);

        var superAdminToken = await TestAuth.GetTokenAsync(_client, "SuperAdmin");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Users/search?searchTerm={email}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
        var users = await (await _client.SendAsync(request)).Content.ReadFromJsonAsync<List<UserDto>>();

        return (token, users!.Single().Id);
    }

    [Fact]
    public async Task DeactivatedUser_TokenStopsWorkingImmediately()
    {
        var (token, userId) = await CreateManagerAsync();
        (await GetStatusAsync("/api/Hotels", token)).Should().Be(HttpStatusCode.OK);

        (await SendAsSuperAdminAsync(HttpMethod.Post, $"/api/Users/{userId}/deactivate")).Should().Be(HttpStatusCode.OK);

        (await GetStatusAsync("/api/Hotels", token)).Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RoleChange_EndsExistingSessions()
    {
        var (token, userId) = await CreateManagerAsync();

        (await SendAsSuperAdminAsync(HttpMethod.Patch, $"/api/Users/{userId}/role", new { Role = "Guest" }))
            .Should().Be(HttpStatusCode.OK);

        // The old token still claims Manager; it must no longer be accepted
        (await GetStatusAsync("/api/Hotels", token)).Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RepeatedWrongPasswords_LockTheAccount()
    {
        var email = $"lockout{Guid.NewGuid():N}@test.com";
        await TestAuth.GetTokenAsync(_client, "Guest", email);

        for (var i = 0; i < 5; i++)
            await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequestDto { Email = email, Password = "wrong-password1" });

        // Even the right password is refused while locked out
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequestDto { Email = email, Password = "Test123" });
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
