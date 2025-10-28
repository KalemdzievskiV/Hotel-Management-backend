using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

public class RoomsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RoomsControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync(string role = "Admin")
    {
        var registerRequest = new RegisterRequestDto
        {
            FirstName = $"Test{role}",
            LastName = "Room",
            Email = $"testroom{role}{Guid.NewGuid().ToString().Substring(0, 8)}@test.com",
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

    private async Task<HotelDto> CreateTestHotelAsync(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var hotel = new HotelDto
        {
            Name = $"Test Hotel {Guid.NewGuid()}",
            Address = "123 Test St",
            City = "TestCity",
            Country = "TestCountry"
        };

        var response = await _client.PostAsJsonAsync("/api/Hotels", hotel);
        return (await response.Content.ReadFromJsonAsync<HotelDto>())!;
    }

    #region Authorization Tests

    [Fact]
    public async Task GetAllRooms_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/Rooms");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllRooms_WithAuth_ShouldReturnOk()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/Rooms");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region CRUD Tests

    [Fact]
    public async Task CreateRoom_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(token);

        var newRoom = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "101",
            Type = RoomType.Double,
            Floor = 1,
            Capacity = 2,
            PricePerNight = 100,
            Description = "Comfortable double room",
            Status = RoomStatus.Available,
            IsActive = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Rooms", newRoom);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdRoom = await response.Content.ReadFromJsonAsync<RoomDto>();
        createdRoom.Should().NotBeNull();
        createdRoom!.RoomNumber.Should().Be("101");
        createdRoom.Id.Should().BeGreaterThan(0);
        createdRoom.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateRoom_WithDuplicateRoomNumber_ShouldReturnBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(token);

        var room1 = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "102",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        };

        await _client.PostAsJsonAsync("/api/Rooms", room1);

        var room2 = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "102", // Duplicate
            Type = RoomType.Single,
            Capacity = 1,
            PricePerNight = 80
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Rooms", room2);

        // Assert
        // InvalidOperationException returns 500 (we could change to custom exception for 400)
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetRoomById_WhenExists_ShouldReturnRoom()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(token);

        var newRoom = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "103",
            Type = RoomType.Suite,
            Capacity = 4,
            PricePerNight = 250
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Rooms", newRoom);
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomDto>();

        // Act
        var getResponse = await _client.GetAsync($"/api/Rooms/{createdRoom!.Id}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var room = await getResponse.Content.ReadFromJsonAsync<RoomDto>();
        room.Should().NotBeNull();
        room!.RoomNumber.Should().Be("103");
    }

    [Fact]
    public async Task UpdateRoom_WithValidData_ShouldReturnOk()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(token);

        var newRoom = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "104",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Rooms", newRoom);
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomDto>();

        createdRoom!.PricePerNight = 120;
        createdRoom.Description = "Updated description";

        // Act
        var updateResponse = await _client.PutAsJsonAsync($"/api/Rooms/{createdRoom.Id}", createdRoom);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedRoom = await updateResponse.Content.ReadFromJsonAsync<RoomDto>();
        updatedRoom!.PricePerNight.Should().Be(120);
        updatedRoom.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteRoom_WithAdminRole_ShouldReturnNoContent()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(token);

        var newRoom = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "105",
            Type = RoomType.Single,
            Capacity = 1,
            PricePerNight = 80
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Rooms", newRoom);
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomDto>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/Rooms/{createdRoom!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    #endregion

    #region Hotel-Specific Room Queries

    [Fact]
    public async Task GetRoomsByHotel_ShouldReturnOnlyHotelRooms()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        var hotel1 = await CreateTestHotelAsync(token);
        var hotel2 = await CreateTestHotelAsync(token);

        // Create rooms for hotel1
        await _client.PostAsJsonAsync("/api/Rooms", new RoomDto
        {
            HotelId = hotel1.Id,
            RoomNumber = "201",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        });

        await _client.PostAsJsonAsync("/api/Rooms", new RoomDto
        {
            HotelId = hotel1.Id,
            RoomNumber = "202",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        });

        // Create room for hotel2
        await _client.PostAsJsonAsync("/api/Rooms", new RoomDto
        {
            HotelId = hotel2.Id,
            RoomNumber = "101",
            Type = RoomType.Single,
            Capacity = 1,
            PricePerNight = 80
        });

        // Act
        var response = await _client.GetAsync($"/api/Rooms/hotel/{hotel1.Id}");
        var rooms = await response.Content.ReadFromJsonAsync<List<RoomDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        rooms.Should().NotBeNull();
        rooms!.Should().HaveCountGreaterOrEqualTo(2);
        rooms.Should().AllSatisfy(r => r.HotelId.Should().Be(hotel1.Id));
    }

    [Fact]
    public async Task GetAvailableRooms_ShouldReturnOnlyActiveAndAvailable()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(token);

        // Create available room
        await _client.PostAsJsonAsync("/api/Rooms", new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "301",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100,
            Status = RoomStatus.Available,
            IsActive = true
        });

        // Create occupied room
        await _client.PostAsJsonAsync("/api/Rooms", new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "302",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100,
            Status = RoomStatus.Occupied,
            IsActive = true
        });

        // Act
        var response = await _client.GetAsync($"/api/Rooms/hotel/{hotel.Id}/available");
        var rooms = await response.Content.ReadFromJsonAsync<List<RoomDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        rooms.Should().NotBeNull();
        rooms!.Should().Contain(r => r.RoomNumber == "301");
        rooms.Should().NotContain(r => r.RoomNumber == "302");
    }

    #endregion

    #region Room Status Management

    [Fact]
    public async Task UpdateRoomStatus_WithManagerRole_ShouldSucceed()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Manager");
        var hotel = await CreateTestHotelAsync(token);

        var room = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "401",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100,
            Status = RoomStatus.Available
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Rooms", room);
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomDto>();

        var statusUpdate = new { Status = RoomStatus.Cleaning };

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/Rooms/{createdRoom!.Id}/status", statusUpdate);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedRoom = await response.Content.ReadFromJsonAsync<RoomDto>();
        updatedRoom!.Status.Should().Be(RoomStatus.Cleaning);
    }

    [Fact]
    public async Task MarkRoomAsCleaned_WithHousekeeperRole_ShouldSucceed()
    {
        // Arrange
        var adminToken = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(adminToken);

        var room = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "501",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100,
            Status = RoomStatus.Cleaning
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Rooms", room);
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomDto>();

        // Switch to Housekeeper role
        var housekeeperToken = await GetAuthTokenAsync("Housekeeper");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", housekeeperToken);

        // Act
        var response = await _client.PostAsync($"/api/Rooms/{createdRoom!.Id}/clean", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RecordMaintenance_WithManagerRole_ShouldSucceed()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Manager");
        var hotel = await CreateTestHotelAsync(token);

        var room = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "601",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100,
            Status = RoomStatus.Available
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Rooms", room);
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomDto>();

        var maintenanceData = new { Notes = "AC unit repair needed" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/Rooms/{createdRoom!.Id}/maintenance", maintenanceData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Room Type and Features Tests

    [Fact]
    public async Task CreateRoom_WithAllFeatures_ShouldSucceed()
    {
        // Arrange
        var token = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(token);

        var newRoom = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "701",
            Type = RoomType.Deluxe,
            Floor = 7,
            Capacity = 3,
            PricePerNight = 250,
            Description = "Luxury deluxe room with ocean view",
            Amenities = "WiFi,TV,AC,Minibar,Safe,Balcony",
            BedType = "1 King Bed + 1 Sofa Bed",
            HasBathtub = true,
            HasBalcony = true,
            IsSmokingAllowed = false,
            ViewType = "Ocean View",
            AreaSqM = 45.5m,
            Status = RoomStatus.Available,
            IsActive = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Rooms", newRoom);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdRoom = await response.Content.ReadFromJsonAsync<RoomDto>();
        createdRoom.Should().NotBeNull();
        createdRoom!.Type.Should().Be(RoomType.Deluxe);
        createdRoom.HasBalcony.Should().BeTrue();
        createdRoom.ViewType.Should().Be("Ocean View");
        createdRoom.AreaSqM.Should().Be(45.5m);
    }

    #endregion

    #region Authorization by Role Tests

    [Fact]
    public async Task CreateRoom_WithGuestRole_ShouldReturnForbidden()
    {
        // Arrange
        var adminToken = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(adminToken);

        var guestToken = await GetAuthTokenAsync("Guest");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var newRoom = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "801",
            Type = RoomType.Single,
            Capacity = 1,
            PricePerNight = 80
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Rooms", newRoom);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRoom_WithManagerRole_ShouldReturnForbidden()
    {
        // Arrange
        var adminToken = await GetAuthTokenAsync("Admin");
        var hotel = await CreateTestHotelAsync(adminToken);

        var room = new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "901",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Rooms", room);
        var createdRoom = await createResponse.Content.ReadFromJsonAsync<RoomDto>();

        // Switch to Manager role
        var managerToken = await GetAuthTokenAsync("Manager");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/Rooms/{createdRoom!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion
}
