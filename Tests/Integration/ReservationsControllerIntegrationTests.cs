using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace HotelManagement.Tests.Integration;

public class ReservationsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private string _adminToken = string.Empty;
    private int _hotelId;
    private int _roomId;
    private int _guestId;

    public ReservationsControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task SeedTestData()
    {
        // Register admin user via API
        var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", new RegisterRequestDto
        {
            FirstName = "Admin",
            LastName = "Test",
            Email = $"admin{Guid.NewGuid():N}@test.com",
            Password = "Admin123!",
            Role = "Guest" // Will need to be SuperAdmin
        });

        // Try to login with SuperAdmin from seeder
        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            email = "admin@admin.com",
            password = "Admin123!"
        });

        if (!loginResponse.IsSuccessStatusCode)
        {
            throw new Exception("Failed to login as super admin");
        }

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        _adminToken = loginResult!.Token;

        // Set authorization header
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create hotel via API
        var hotelDto = new HotelDto
        {
            Name = "Test Hotel",
            Address = "123 Test St",
            City = "Test City",
            Country = "Test Country",
            PostalCode = "12345"
        };
        var hotelResponse = await _client.PostAsJsonAsync("/api/Hotels", hotelDto);
        var hotel = await hotelResponse.Content.ReadFromJsonAsync<HotelDto>();
        _hotelId = hotel!.Id;

        // Create room via API
        var roomDto = new RoomDto
        {
            HotelId = _hotelId,
            RoomNumber = "101",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 150,
            Status = RoomStatus.Available,
            AllowsShortStay = true,
            ShortStayHourlyRate = 25,
            MinimumShortStayHours = 2,
            MaximumShortStayHours = 12
        };
        var roomResponse = await _client.PostAsJsonAsync("/api/Rooms", roomDto);
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomDto>();
        _roomId = room!.Id;

        // Create guest via API
        var guestDto = new GuestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            PhoneNumber = "+1-555-0100"
        };
        var guestResponse = await _client.PostAsJsonAsync("/api/Guests", guestDto);
        var guest = await guestResponse.Content.ReadFromJsonAsync<GuestDto>();
        _guestId = guest!.Id;
    }

    [Fact]
    public async Task CreateReservation_ValidDailyBooking_Returns201()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2,
            DepositAmount = 100,
            PaymentMethod = PaymentMethod.CreditCard
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Reservations", createDto);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.NotNull(result);
        Assert.Equal(300, result.TotalAmount); // 2 nights × $150
        Assert.Equal(100, result.DepositAmount);
        Assert.Equal(200, result.RemainingAmount);
        Assert.Equal(ReservationStatus.Pending, result.Status);
        Assert.Equal(PaymentStatus.PartiallyPaid, result.PaymentStatus);
    }

    [Fact]
    public async Task CreateReservation_ValidShortStayBooking_Returns201()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var checkIn = DateTime.UtcNow.AddDays(5);
        var checkOut = checkIn.AddHours(4);

        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.ShortStay,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = 2,
            DepositAmount = 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Reservations", createDto);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalAmount); // 4 hours × $25
        Assert.Equal(4, result.DurationInHours);
        Assert.Equal(PaymentStatus.Unpaid, result.PaymentStatus);
    }

    [Fact]
    public async Task CreateReservation_Unauthorized_Returns401()
    {
        // Arrange
        await SeedTestData();
        // Don't set authorization header

        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Reservations", createDto);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetReservationById_ExistingReservation_Returns200()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create reservation first
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Reservations", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationDto>();

        // Act
        var response = await _client.GetAsync($"/api/Reservations/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task GetReservationById_NonExistent_Returns404()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _client.GetAsync("/api/Reservations/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CheckRoomAvailability_AvailableRoom_ReturnsTrue()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var checkIn = DateTime.UtcNow.AddDays(10);
        var checkOut = DateTime.UtcNow.AddDays(12);

        // Act
        var response = await _client.GetAsync(
            $"/api/Reservations/room/{_roomId}/availability?checkIn={checkIn:O}&checkOut={checkOut:O}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        Assert.NotNull(result);
        Assert.True((bool)result.GetProperty("isAvailable"));
    }

    [Fact]
    public async Task CheckRoomAvailability_ConflictingReservation_ReturnsFalse()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create existing reservation
        var existingDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(8),
            NumberOfGuests = 2
        };
        await _client.PostAsJsonAsync("/api/Reservations", existingDto);

        // Check availability for overlapping dates
        var checkIn = DateTime.UtcNow.AddDays(6);
        var checkOut = DateTime.UtcNow.AddDays(9);

        // Act
        var response = await _client.GetAsync(
            $"/api/Reservations/room/{_roomId}/availability?checkIn={checkIn:O}&checkOut={checkOut:O}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        Assert.NotNull(result);
        Assert.False((bool)result.GetProperty("isAvailable"));
    }

    [Fact]
    public async Task ConfirmReservation_PendingReservation_Returns200()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create pending reservation
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Reservations", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationDto>();

        // Act
        var response = await _client.PostAsync($"/api/Reservations/{created!.Id}/confirm", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.NotNull(result);
        Assert.Equal(ReservationStatus.Confirmed, result.Status);
        Assert.NotNull(result.ConfirmedAt);
    }

    [Fact]
    public async Task CheckInReservation_ConfirmedReservation_Returns200()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create and confirm reservation with past check-in date
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(-1), // Past date
            CheckOutDate = DateTime.UtcNow.AddDays(2),
            NumberOfGuests = 2
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Reservations", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationDto>();

        // Confirm it
        await _client.PostAsync($"/api/Reservations/{created!.Id}/confirm", null);

        // Act
        var response = await _client.PostAsync($"/api/Reservations/{created.Id}/checkin", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.NotNull(result);
        Assert.Equal(ReservationStatus.CheckedIn, result.Status);
        Assert.NotNull(result.CheckedInAt);
    }

    [Fact]
    public async Task CheckOutReservation_CheckedInReservation_Returns200()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create, confirm, and check-in reservation
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(-2),
            CheckOutDate = DateTime.UtcNow.AddDays(1),
            NumberOfGuests = 2
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Reservations", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationDto>();

        await _client.PostAsync($"/api/Reservations/{created!.Id}/confirm", null);
        await _client.PostAsync($"/api/Reservations/{created.Id}/checkin", null);

        // Act
        var response = await _client.PostAsync($"/api/Reservations/{created.Id}/checkout", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.NotNull(result);
        Assert.Equal(ReservationStatus.CheckedOut, result.Status);
        Assert.NotNull(result.CheckedOutAt);
    }

    [Fact]
    public async Task CancelReservation_PendingReservation_Returns200()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create reservation
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2,
            DepositAmount = 100
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Reservations", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationDto>();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/Reservations/{created!.Id}/cancel", 
            new { reason = "Guest requested" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.NotNull(result);
        Assert.Equal(ReservationStatus.Cancelled, result.Status);
        Assert.Equal("Guest requested", result.CancellationReason);
        Assert.NotNull(result.CancelledAt);
    }

    [Fact]
    public async Task RecordPayment_ValidAmount_Returns200()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create reservation with partial payment
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2,
            DepositAmount = 100 // Partial
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Reservations", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationDto>();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/Reservations/{created!.Id}/payment", new
        {
            amount = 200, // Pay remaining
            paymentMethod = PaymentMethod.Cash,
            reference = "CASH-123"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ReservationDto>();
        Assert.NotNull(result);
        Assert.Equal(300, result.DepositAmount); // 100 + 200
        Assert.Equal(0, result.RemainingAmount);
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
    }

    [Fact]
    public async Task GetReservationsByHotel_ReturnsFiltered()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create reservation
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2
        };
        await _client.PostAsJsonAsync("/api/Reservations", createDto);

        // Act
        var response = await _client.GetAsync($"/api/Reservations/hotel/{_hotelId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<List<ReservationDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, r => Assert.Equal(_hotelId, r.HotelId));
    }

    [Fact]
    public async Task GetReservationsByStatus_ReturnsFiltered()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create reservation
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2
        };
        await _client.PostAsJsonAsync("/api/Reservations", createDto);

        // Act
        var response = await _client.GetAsync($"/api/Reservations/status/{ReservationStatus.Pending}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<List<ReservationDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, r => Assert.Equal(ReservationStatus.Pending, r.Status));
    }

    [Fact]
    public async Task GetStatistics_Count_ReturnsTotal()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create reservations
        for (int i = 0; i < 3; i++)
        {
            var createDto = new CreateReservationDto
            {
                HotelId = _hotelId,
                RoomId = _roomId,
                GuestId = _guestId,
                BookingType = BookingType.Daily,
                CheckInDate = DateTime.UtcNow.AddDays(5 + i),
                CheckOutDate = DateTime.UtcNow.AddDays(7 + i),
                NumberOfGuests = 2
            };
            await _client.PostAsJsonAsync("/api/Reservations", createDto);
        }

        // Act
        var response = await _client.GetAsync("/api/Reservations/stats/count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        Assert.NotNull(result);
        Assert.Equal(3, (int)result.GetProperty("totalReservations"));
    }

    [Fact]
    public async Task GetStatistics_ByStatus_ReturnsBreakdown()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create reservations
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2
        };
        await _client.PostAsJsonAsync("/api/Reservations", createDto);

        // Act
        var response = await _client.GetAsync("/api/Reservations/stats/by-status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("Pending") || result.ContainsKey("0")); // Enum might be serialized as int or string
    }

    [Fact]
    public async Task DeleteReservation_PendingReservation_Returns204()
    {
        // Arrange
        await SeedTestData();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        // Create reservation
        var createDto = new CreateReservationDto
        {
            HotelId = _hotelId,
            RoomId = _roomId,
            GuestId = _guestId,
            BookingType = BookingType.Daily,
            CheckInDate = DateTime.UtcNow.AddDays(5),
            CheckOutDate = DateTime.UtcNow.AddDays(7),
            NumberOfGuests = 2
        };
        var createResponse = await _client.PostAsJsonAsync("/api/Reservations", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<ReservationDto>();

        // Act
        var response = await _client.DeleteAsync($"/api/Reservations/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it's deleted
        var getResponse = await _client.GetAsync($"/api/Reservations/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
