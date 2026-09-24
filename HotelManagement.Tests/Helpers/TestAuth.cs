using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;

namespace HotelManagement.Tests.Helpers;

/// <summary>
/// Obtains JWTs for integration tests. Guests self-register; staff accounts can only be
/// created by a SuperAdmin through /api/Users, mirroring how the real app works.
/// </summary>
public static class TestAuth
{
    // Seeded on startup in non-production environments (see Program.cs / DbSeeder)
    public const string SuperAdminEmail = "superadmin@hotel.com";
    public const string SuperAdminPassword = "SuperAdmin123!";
    private const string TestPassword = "Test123";

    /// <summary>
    /// Returns a token for a user with the given role. Pass a fixed email to reuse the same
    /// user across calls; omit it to get a fresh user. Staff can be assigned to a hotel,
    /// which is what gives Managers and Housekeepers access to it.
    /// </summary>
    public static async Task<string> GetTokenAsync(HttpClient client, string role, string? email = null, int? hotelId = null)
    {
        if (role == AppRoles.SuperAdmin)
            return await LoginAsync(client, SuperAdminEmail, SuperAdminPassword);

        email ??= $"test{role.ToLowerInvariant()}{Guid.NewGuid():N}@test.com";

        // Both calls fail harmlessly with 400 when the user already exists
        if (role == AppRoles.Guest)
        {
            await client.PostAsJsonAsync("/api/Auth/register", new RegisterRequestDto
            {
                FirstName = "Test",
                LastName = role,
                Email = email,
                Password = TestPassword,
                Role = role
            });
        }
        else
        {
            var superAdminToken = await LoginAsync(client, SuperAdminEmail, SuperAdminPassword);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Users")
            {
                Content = JsonContent.Create(new CreateUserDto
                {
                    FirstName = "Test",
                    LastName = role,
                    Email = email,
                    Password = TestPassword,
                    Role = role,
                    HotelId = hotelId
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superAdminToken);
            await client.SendAsync(request);
        }

        return await LoginAsync(client, email, TestPassword);
    }

    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return auth!.Token;
    }
}
