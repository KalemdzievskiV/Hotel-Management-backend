using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

public class GuestsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GuestsControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync(string role = "Admin")
    {
        var registerRequest = new RegisterRequestDto
        {
            FirstName = $"Test{role}",
            LastName = "Guest",
            Email = $"testguest{role}{Guid.NewGuid().ToString().Substring(0, 8)}@test.com",
            Password = "Test123",
            Role = role
        };

        await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);

        var loginRequest = new LoginRequestDto
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        return authResponse!.Token;
    }

    #region Authorization Tests

    [Fact]
    public async Task GetAllGuests_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/Guests");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllGuests_WithAuth_ShouldReturnOk()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/Guests");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region CRUD Tests

    [Fact]
    public async Task CreateGuest_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newGuest = new GuestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = $"john.doe{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-0100",
            DateOfBirth = new DateTime(1990, 5, 15),
            Nationality = "American",
            Address = "123 Main St",
            City = "New York",
            Country = "USA",
            PostalCode = "10001"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Guests", newGuest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdGuest = await response.Content.ReadFromJsonAsync<GuestDto>();
        createdGuest.Should().NotBeNull();
        createdGuest!.FirstName.Should().Be("John");
        createdGuest.LastName.Should().Be("Doe");
        createdGuest.Age.Should().BeGreaterThan(18);
        createdGuest.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateGuest_WithDuplicateEmail_ShouldReturnError()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var email = $"duplicate{Guid.NewGuid().ToString().Substring(0, 8)}@example.com";

        var guest1 = new GuestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = email,
            PhoneNumber = "+1-555-0100"
        };

        await _client.PostAsJsonAsync("/api/Guests", guest1);

        var guest2 = new GuestDto
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = email,  // Duplicate
            PhoneNumber = "+1-555-0200"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Guests", guest2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError); // Email uniqueness violation
    }

    [Fact]
    public async Task GetGuestById_WhenExists_ShouldReturnGuest()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newGuest = new GuestDto
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = $"jane.smith{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-0200"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Guests", newGuest);
        var createdGuest = await createResponse.Content.ReadFromJsonAsync<GuestDto>();

        // Act
        var getResponse = await _client.GetAsync($"/api/Guests/{createdGuest!.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var guest = await getResponse.Content.ReadFromJsonAsync<GuestDto>();
        guest.Should().NotBeNull();
        guest!.Email.Should().Be(newGuest.Email);
    }

    [Fact]
    public async Task UpdateGuest_WithValidData_ShouldReturnOk()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newGuest = new GuestDto
        {
            FirstName = "Bob",
            LastName = "Johnson",
            Email = $"bob.johnson{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-0300"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Guests", newGuest);
        var createdGuest = await createResponse.Content.ReadFromJsonAsync<GuestDto>();

        createdGuest!.PhoneNumber = "+1-555-0399";
        createdGuest.Address = "456 Oak Ave";

        // Act
        var updateResponse = await _client.PutAsJsonAsync($"/api/Guests/{createdGuest.Id}", createdGuest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedGuest = await updateResponse.Content.ReadFromJsonAsync<GuestDto>();
        updatedGuest!.PhoneNumber.Should().Be("+1-555-0399");
        updatedGuest.Address.Should().Be("456 Oak Ave");
    }

    [Fact]
    public async Task DeleteGuest_WithAdminRole_ShouldReturnNoContent()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newGuest = new GuestDto
        {
            FirstName = "Delete",
            LastName = "Test",
            Email = $"delete.test{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-0400"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Guests", newGuest);
        var createdGuest = await createResponse.Content.ReadFromJsonAsync<GuestDto>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/Guests/{createdGuest!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    #endregion

    #region Search Tests

    [Fact]
    public async Task SearchByName_ShouldReturnMatchingGuests()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var uniqueSurname = $"SearchTest{Guid.NewGuid().ToString().Substring(0, 6)}";

        await _client.PostAsJsonAsync("/api/Guests", new GuestDto
        {
            FirstName = "Alice",
            LastName = uniqueSurname,
            Email = $"alice{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-0500"
        });

        await _client.PostAsJsonAsync("/api/Guests", new GuestDto
        {
            FirstName = "Bob",
            LastName = uniqueSurname,
            Email = $"bob{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-0501"
        });

        // Act
        var response = await _client.GetAsync($"/api/Guests/search?name={uniqueSurname}");
        var guests = await response.Content.ReadFromJsonAsync<List<GuestDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        guests.Should().NotBeNull();
        guests!.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetByEmail_WhenExists_ShouldReturnGuest()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var email = $"findme{Guid.NewGuid().ToString().Substring(0, 8)}@example.com";

        await _client.PostAsJsonAsync("/api/Guests", new GuestDto
        {
            FirstName = "Find",
            LastName = "Me",
            Email = email,
            PhoneNumber = "+1-555-0600"
        });

        // Act
        var response = await _client.GetAsync($"/api/Guests/email/{email}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var guest = await response.Content.ReadFromJsonAsync<GuestDto>();
        guest.Should().NotBeNull();
        guest!.Email.Should().Be(email);
    }

    #endregion

    #region VIP Management Tests

    [Fact]
    public async Task SetVIPStatus_ShouldUpdateGuestVIPFlag()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newGuest = new GuestDto
        {
            FirstName = "VIP",
            LastName = "Guest",
            Email = $"vip.guest{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-0700",
            IsVIP = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Guests", newGuest);
        var createdGuest = await createResponse.Content.ReadFromJsonAsync<GuestDto>();

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/Guests/{createdGuest!.Id}/vip", new { IsVIP = true });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify VIP status was updated
        var getResponse = await _client.GetAsync($"/api/Guests/{createdGuest.Id}");
        var updatedGuest = await getResponse.Content.ReadFromJsonAsync<GuestDto>();
        updatedGuest!.IsVIP.Should().BeTrue();
    }

    [Fact]
    public async Task GetVIPGuests_ShouldReturnOnlyVIPGuests()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create VIP guest
        await _client.PostAsJsonAsync("/api/Guests", new GuestDto
        {
            FirstName = "VIP",
            LastName = "One",
            Email = $"vip1{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-0800",
            IsVIP = true
        });

        // Act
        var response = await _client.GetAsync("/api/Guests/vip");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var guests = await response.Content.ReadFromJsonAsync<List<GuestDto>>();
        guests.Should().NotBeNull();
        guests!.Should().Contain(g => g.IsVIP);
    }

    #endregion

    #region Blacklist Management Tests

    [Fact]
    public async Task BlacklistGuest_ShouldSetBlacklistFlag()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newGuest = new GuestDto
        {
            FirstName = "Blacklist",
            LastName = "Test",
            Email = $"blacklist{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-0900"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Guests", newGuest);
        var createdGuest = await createResponse.Content.ReadFromJsonAsync<GuestDto>();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/Guests/{createdGuest!.Id}/blacklist", new { Reason = "Payment fraud" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify blacklist status
        var getResponse = await _client.GetAsync($"/api/Guests/{createdGuest.Id}");
        var updatedGuest = await getResponse.Content.ReadFromJsonAsync<GuestDto>();
        updatedGuest!.IsBlacklisted.Should().BeTrue();
        updatedGuest.BlacklistReason.Should().Be("Payment fraud");
    }

    [Fact]
    public async Task UnblacklistGuest_ShouldClearBlacklistFlag()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newGuest = new GuestDto
        {
            FirstName = "Unblacklist",
            LastName = "Test",
            Email = $"unblacklist{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-1000",
            IsBlacklisted = true,
            BlacklistReason = "Previous issue"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Guests", newGuest);
        var createdGuest = await createResponse.Content.ReadFromJsonAsync<GuestDto>();

        // Act
        var response = await _client.PostAsync($"/api/Guests/{createdGuest!.Id}/unblacklist", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify blacklist cleared
        var getResponse = await _client.GetAsync($"/api/Guests/{createdGuest.Id}");
        var updatedGuest = await getResponse.Content.ReadFromJsonAsync<GuestDto>();
        updatedGuest!.IsBlacklisted.Should().BeFalse();
        updatedGuest.BlacklistReason.Should().BeNullOrEmpty();
    }

    #endregion

    #region Authorization by Role Tests

    [Fact]
    public async Task CreateGuest_WithGuestRole_ShouldReturnForbidden()
    {
        // Arrange
        var guestToken = await GetAuthTokenAsync("Guest");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var newGuest = new GuestDto
        {
            FirstName = "Test",
            LastName = "Guest",
            Email = $"test{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-1100"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Guests", newGuest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteGuest_WithManagerRole_ShouldReturnForbidden()
    {
        // Arrange
        var adminToken = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var newGuest = new GuestDto
        {
            FirstName = "Delete",
            LastName = "Test",
            Email = $"deletetest{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-1200"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Guests", newGuest);
        var createdGuest = await createResponse.Content.ReadFromJsonAsync<GuestDto>();

        // Switch to Manager role
        var managerToken = await GetAuthTokenAsync("Manager");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/Guests/{createdGuest!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Guest with Full Details Tests

    [Fact]
    public async Task CreateGuest_WithAllDetails_ShouldSucceed()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var comprehensiveGuest = new GuestDto
        {
            FirstName = "Michael",
            LastName = "Johnson",
            Email = $"michael.johnson{Guid.NewGuid().ToString().Substring(0, 8)}@example.com",
            PhoneNumber = "+1-555-1300",
            IdentificationNumber = "P1234567",
            IdentificationType = "Passport",
            DateOfBirth = new DateTime(1985, 3, 20),
            Nationality = "Canadian",
            Gender = "Male",
            Address = "789 Elm Street",
            City = "Toronto",
            State = "Ontario",
            Country = "Canada",
            PostalCode = "M5H 2N2",
            EmergencyContactName = "Sarah Johnson",
            EmergencyContactPhone = "+1-555-1301",
            EmergencyContactRelationship = "Spouse",
            SpecialRequests = "Vegetarian meals, ground floor room preferred",
            Preferences = "Non-smoking, quiet room",
            IsVIP = true,
            LoyaltyProgramNumber = "LP123456",
            EmailNotifications = true,
            SmsNotifications = true,
            PreferredLanguage = "en",
            CompanyName = "Tech Corp",
            TaxId = "TAX123456"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Guests", comprehensiveGuest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdGuest = await response.Content.ReadFromJsonAsync<GuestDto>();
        createdGuest.Should().NotBeNull();
        createdGuest!.IdentificationNumber.Should().Be("P1234567");
        createdGuest.IsVIP.Should().BeTrue();
        createdGuest.CompanyName.Should().Be("Tech Corp");
        createdGuest.Age.Should().BeGreaterThan(18);
    }

    #endregion
}
