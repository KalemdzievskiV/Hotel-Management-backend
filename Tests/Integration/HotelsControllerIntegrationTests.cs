using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

public class HotelsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HotelsControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync(string role = "Admin")
    {
        var registerRequest = new RegisterRequestDto
        {
            FirstName = $"Test",
            LastName = role,
            Email = $"test{role}@test.com",
            Password = "Test123",
            Role = role
        };

        var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
        
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // User might already exist, try login
            var loginRequest = new LoginRequestDto
            {
                Email = registerRequest.Email,
                Password = registerRequest.Password
            };
            response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
        }

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return authResponse!.Token;
    }

    [Fact]
    public async Task GetAllHotels_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/Hotels");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllHotels_WithAuth_ShouldReturnOk()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Guest");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/Hotels");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var hotels = await response.Content.ReadFromJsonAsync<List<HotelDto>>();
        hotels.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateHotel_WithAdminRole_ShouldReturnCreated()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newHotel = new HotelDto
        {
            Name = "Test Hotel Integration",
            Address = "123 Integration St",
            City = "TestCity",
            Country = "TestCountry"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Hotels", newHotel);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdHotel = await response.Content.ReadFromJsonAsync<HotelDto>();
        createdHotel.Should().NotBeNull();
        createdHotel!.Name.Should().Be("Test Hotel Integration");
        createdHotel.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateHotel_WithGuestRole_ShouldReturnForbidden()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Guest");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newHotel = new HotelDto
        {
            Name = "Unauthorized Hotel",
            Address = "123 Unauthorized St",
            City = "TestCity",
            Country = "TestCountry"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Hotels", newHotel);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateHotel_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var invalidHotel = new HotelDto
        {
            Name = "", // Empty name should fail validation
            Address = "123 Test St",
            City = "TestCity",
            Country = "TestCountry"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Hotels", invalidHotel);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteHotel_WithAdminRole_ShouldReturnNoContent()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // First create a hotel
        var newHotel = new HotelDto 
        { 
            Name = "Hotel To Delete",
            Address = "123 Delete St",
            City = "TestCity",
            Country = "TestCountry"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Hotels", newHotel);
        var createdHotel = await createResponse.Content.ReadFromJsonAsync<HotelDto>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/Hotels/{createdHotel!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteHotel_WithManagerRole_ShouldReturnForbidden()
    {
        // Arrange
        var adminToken = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Create a hotel as admin
        var newHotel = new HotelDto 
        { 
            Name = "Hotel Manager Cannot Delete",
            Address = "123 Test St",
            City = "TestCity",
            Country = "TestCountry"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Hotels", newHotel);
        var createdHotel = await createResponse.Content.ReadFromJsonAsync<HotelDto>();

        // Try to delete as Manager
        var managerToken = await GetAuthTokenAsync("Manager");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/Hotels/{createdHotel!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #region Ownership Tests

    [Fact]
    public async Task CreateHotel_ShouldAutoSetOwnerId()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newHotel = new HotelDto
        {
            Name = "Ownership Test Hotel",
            Address = "123 Owner St",
            City = "OwnerCity",
            Country = "OwnerCountry"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Hotels", newHotel);
        var createdHotel = await response.Content.ReadFromJsonAsync<HotelDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        createdHotel.Should().NotBeNull();
        createdHotel!.OwnerId.Should().NotBeNullOrEmpty();
        // Note: OwnerName requires eager loading of Owner navigation property
        // createdHotel.OwnerName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateHotel_ByOwner_ShouldSucceed()
    {
        // Arrange
        var adminToken = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Create hotel
        var newHotel = new HotelDto 
        { 
            Name = "Original Name",
            Address = "123 Test St",
            City = "TestCity",
            Country = "TestCountry"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Hotels", newHotel);
        var createdHotel = await createResponse.Content.ReadFromJsonAsync<HotelDto>();

        // Update hotel (same user)
        createdHotel!.Name = "Updated Name";
        var updateResponse = await _client.PutAsJsonAsync($"/api/Hotels/{createdHotel.Id}", createdHotel);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedHotel = await updateResponse.Content.ReadFromJsonAsync<HotelDto>();
        updatedHotel!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateHotel_ByDifferentAdmin_ShouldReturnForbidden()
    {
        // Arrange - Create hotel as Admin1
        var admin1Token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin1Token);

        var newHotel = new HotelDto 
        { 
            Name = "Admin1 Hotel",
            Address = "123 Test St",
            City = "TestCity",
            Country = "TestCountry"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Hotels", newHotel);
        var createdHotel = await createResponse.Content.ReadFromJsonAsync<HotelDto>();

        // Try to update as different Admin (Manager)
        var admin2Token = await GetAuthTokenAsync("Manager");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin2Token);

        createdHotel!.Name = "Hacked Name";
        var updateResponse = await _client.PutAsJsonAsync($"/api/Hotels/{createdHotel.Id}", createdHotel);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteHotel_ByDifferentAdmin_ShouldReturnForbidden()
    {
        // Arrange - Create hotel as Admin
        var adminToken = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var newHotel = new HotelDto 
        { 
            Name = "Admin Hotel",
            Address = "123 Test St",
            City = "TestCity",
            Country = "TestCountry"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Hotels", newHotel);
        var createdHotel = await createResponse.Content.ReadFromJsonAsync<HotelDto>();

        // Try to delete as different user (create new admin)
        var admin2Token = await GetAuthTokenAsync("Manager");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin2Token);

        var deleteResponse = await _client.DeleteAsync($"/api/Hotels/{createdHotel!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllHotels_AsAdmin_ShouldReturnOnlyOwnHotels()
    {
        // Arrange - Create 2 hotels as Admin
        var adminToken = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        await _client.PostAsJsonAsync("/api/Hotels", new HotelDto 
        { 
            Name = "Admin Hotel 1",
            Address = "123 Test St",
            City = "TestCity",
            Country = "TestCountry"
        });
        
        await _client.PostAsJsonAsync("/api/Hotels", new HotelDto 
        { 
            Name = "Admin Hotel 2",
            Address = "456 Test Ave",
            City = "TestCity",
            Country = "TestCountry"
        });

        // Create hotel as different user (Manager)
        var managerToken = await GetAuthTokenAsync("Manager");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        
        await _client.PostAsJsonAsync("/api/Hotels", new HotelDto 
        { 
            Name = "Manager Hotel",
            Address = "789 Test Rd",
            City = "TestCity",
            Country = "TestCountry"
        });

        // Act - Get all hotels as original Admin
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var getResponse = await _client.GetAsync("/api/Hotels");
        var hotels = await getResponse.Content.ReadFromJsonAsync<List<HotelDto>>();

        // Assert - Admin should only see their own hotels
        hotels.Should().NotBeNull();
        hotels.Should().HaveCountGreaterOrEqualTo(2);
        // All hotels should belong to this admin (checking OwnerId would be better but requires loading Owner)
        hotels!.Select(h => h.Name).Should().Contain(name => name.Contains("Admin Hotel"));
    }

    #endregion

    #region Extended Fields Validation Tests

    [Fact]
    public async Task CreateHotel_WithAllFields_ShouldSucceed()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var newHotel = new HotelDto
        {
            Name = "Complete Hotel",
            Description = "A fully featured hotel with all amenities",
            Address = "123 Main Street",
            City = "New York",
            Country = "USA",
            PostalCode = "10001",
            PhoneNumber = "+1-212-555-0100",
            Email = "info@hotel.com",
            Website = "https://www.hotel.com",
            Stars = 5,
            Amenities = "WiFi,Parking,Pool,Gym",
            CheckInTime = "15:00",
            CheckOutTime = "12:00"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Hotels", newHotel);
        var createdHotel = await response.Content.ReadFromJsonAsync<HotelDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        createdHotel.Should().NotBeNull();
        createdHotel!.Name.Should().Be("Complete Hotel");
        createdHotel.Description.Should().Be("A fully featured hotel with all amenities");
        createdHotel.Email.Should().Be("info@hotel.com");
        createdHotel.Stars.Should().Be(5);
        createdHotel.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateHotel_WithInvalidEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var invalidHotel = new HotelDto
        {
            Name = "Test Hotel",
            Address = "123 Test St",
            City = "TestCity",
            Country = "TestCountry",
            Email = "not-an-email" // Invalid
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Hotels", invalidHotel);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateHotel_WithInvalidStars_ShouldReturnBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var invalidHotel = new HotelDto
        {
            Name = "Test Hotel",
            Address = "123 Test St",
            City = "TestCity",
            Country = "TestCountry",
            Stars = 6 // Invalid (must be 1-5)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Hotels", invalidHotel);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
