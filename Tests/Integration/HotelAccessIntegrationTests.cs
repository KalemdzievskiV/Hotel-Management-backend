using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Staff of one hotel must not be able to see or change another hotel's data,
/// and guests must only act on their own bookings.
/// </summary>
public class HotelAccessIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HotelAccessIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private record HotelSetup(int HotelId, int RoomId, int GuestId, int ReservationId, string AdminToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await SendRawAsync(method, url, token, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    /// <summary>
    /// A hotel owned by a fresh Admin, with one room, one walk-in guest and one reservation
    /// </summary>
    private async Task<HotelSetup> CreateHotelWithReservationAsync()
    {
        var adminToken = await TestAuth.GetTokenAsync(_client, "Admin");
        var hotel = await SendAsync<HotelDto>(HttpMethod.Post, "/api/Hotels", adminToken, new HotelDto
        {
            Name = $"Access Hotel {Guid.NewGuid():N}",
            Address = "1 Test St",
            City = "TestCity",
            Country = "TestCountry"
        });
        var room = await SendAsync<RoomDto>(HttpMethod.Post, "/api/Rooms", adminToken, new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "101",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        });
        var guest = await SendAsync<GuestDto>(HttpMethod.Post, "/api/Guests", adminToken, new GuestDto
        {
            HotelId = hotel.Id,
            FirstName = "Walk",
            LastName = "In",
            Email = $"walkin{Guid.NewGuid():N}@test.com",
            PhoneNumber = "+1-555-0100"
        });
        var reservation = await SendAsync<ReservationDto>(HttpMethod.Post, "/api/Reservations", adminToken, new CreateReservationDto
        {
            HotelId = hotel.Id,
            RoomId = room.Id,
            GuestId = guest.Id,
            CheckInDate = DateTime.UtcNow.Date.AddDays(10),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(12),
            NumberOfGuests = 1
        });

        return new HotelSetup(hotel.Id, room.Id, guest.Id, reservation.Id, adminToken);
    }

    [Fact]
    public async Task AssignedManager_CanWorkWithTheirHotel()
    {
        var hotelA = await CreateHotelWithReservationAsync();
        var managerToken = await TestAuth.GetTokenAsync(_client, "Manager", hotelId: hotelA.HotelId);

        var hotels = await SendAsync<List<HotelDto>>(HttpMethod.Get, "/api/Hotels", managerToken);
        hotels.Select(h => h.Id).Should().Equal(hotelA.HotelId);

        (await SendRawAsync(HttpMethod.Get, $"/api/Reservations/{hotelA.ReservationId}", managerToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendRawAsync(HttpMethod.Post, $"/api/Reservations/{hotelA.ReservationId}/confirm", managerToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OtherHotelsManager_CannotSeeOrChangeHotelData()
    {
        var hotelA = await CreateHotelWithReservationAsync();
        var hotelB = await CreateHotelWithReservationAsync();
        var managerB = await TestAuth.GetTokenAsync(_client, "Manager", hotelId: hotelB.HotelId);

        (await SendRawAsync(HttpMethod.Get, $"/api/Reservations/{hotelA.ReservationId}", managerB))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendRawAsync(HttpMethod.Post, $"/api/Reservations/{hotelA.ReservationId}/confirm", managerB))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendRawAsync(HttpMethod.Post, $"/api/Reservations/{hotelA.ReservationId}/cancel", managerB, new { Reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendRawAsync(HttpMethod.Patch, $"/api/Rooms/{hotelA.RoomId}/status", managerB, new { Status = RoomStatus.Maintenance }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendRawAsync(HttpMethod.Get, $"/api/Guests/{hotelA.GuestId}", managerB))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var reservations = await SendAsync<List<ReservationDto>>(HttpMethod.Get, "/api/Reservations", managerB);
        reservations.Should().OnlyContain(r => r.HotelId == hotelB.HotelId);

        var guests = await SendAsync<List<GuestDto>>(HttpMethod.Get, "/api/Guests", managerB);
        guests.Should().NotContain(g => g.Id == hotelA.GuestId);
    }

    [Fact]
    public async Task UnassignedManager_SeesNoHotels()
    {
        await CreateHotelWithReservationAsync();
        var managerToken = await TestAuth.GetTokenAsync(_client, "Manager");

        var hotels = await SendAsync<List<HotelDto>>(HttpMethod.Get, "/api/Hotels", managerToken);
        hotels.Should().BeEmpty();
    }

    [Fact]
    public async Task Guest_CannotBookForAnotherGuest_OrTouchOthersReservations()
    {
        var hotelA = await CreateHotelWithReservationAsync();
        var guestToken = await TestAuth.GetTokenAsync(_client, "Guest");

        var booking = new CreateReservationDto
        {
            HotelId = hotelA.HotelId,
            RoomId = hotelA.RoomId,
            GuestId = hotelA.GuestId, // someone else's guest profile
            CheckInDate = DateTime.UtcNow.Date.AddDays(30),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(31),
            NumberOfGuests = 1
        };
        (await SendRawAsync(HttpMethod.Post, "/api/Reservations", guestToken, booking))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await SendRawAsync(HttpMethod.Get, $"/api/Reservations/{hotelA.ReservationId}", guestToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendRawAsync(HttpMethod.Post, $"/api/Reservations/{hotelA.ReservationId}/cancel", guestToken, new { Reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Guest_CanBookForThemselves_AndSeesItInTheirList()
    {
        var hotelA = await CreateHotelWithReservationAsync();
        var guestToken = await TestAuth.GetTokenAsync(_client, "Guest");
        var myProfile = await SendAsync<GuestDto>(HttpMethod.Get, "/api/Guests/me", guestToken);

        var created = await SendAsync<ReservationDto>(HttpMethod.Post, "/api/Reservations", guestToken, new CreateReservationDto
        {
            HotelId = hotelA.HotelId,
            RoomId = hotelA.RoomId,
            GuestId = myProfile.Id,
            CheckInDate = DateTime.UtcNow.Date.AddDays(40),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(41),
            NumberOfGuests = 1
        });

        var mine = await SendAsync<List<ReservationDto>>(HttpMethod.Get, "/api/Reservations", guestToken);
        mine.Select(r => r.Id).Should().Equal(created.Id);
    }

    [Fact]
    public async Task Guest_CannotMarkOwnReservationPaid()
    {
        var hotelA = await CreateHotelWithReservationAsync();
        var guestToken = await TestAuth.GetTokenAsync(_client, "Guest");
        var myProfile = await SendAsync<GuestDto>(HttpMethod.Get, "/api/Guests/me", guestToken);
        var checkIn = DateTime.UtcNow.Date.AddDays(50);
        var created = await SendAsync<ReservationDto>(HttpMethod.Post, "/api/Reservations", guestToken, new CreateReservationDto
        {
            HotelId = hotelA.HotelId,
            RoomId = hotelA.RoomId,
            GuestId = myProfile.Id,
            CheckInDate = checkIn,
            CheckOutDate = checkIn.AddDays(1),
            NumberOfGuests = 1
        });

        // Payments only go through the payment endpoint; a depositAmount in the body is ignored
        var updated = await SendAsync<ReservationDto>(HttpMethod.Put, $"/api/Reservations/{created.Id}", guestToken, new
        {
            CheckInDate = checkIn,
            CheckOutDate = checkIn.AddDays(1),
            NumberOfGuests = 1,
            DepositAmount = created.TotalAmount
        });

        updated.DepositAmount.Should().Be(0);
        updated.PaymentStatus.Should().Be(PaymentStatus.Unpaid);
    }

    [Fact]
    public async Task AssignedHousekeeper_CanSeeTheirHotelsTasks_ButNotOthers()
    {
        var hotelA = await CreateHotelWithReservationAsync();
        var hotelB = await CreateHotelWithReservationAsync();
        var housekeeperA = await TestAuth.GetTokenAsync(_client, "Housekeeper", hotelId: hotelA.HotelId);

        (await SendRawAsync(HttpMethod.Get, $"/api/Housekeeping/hotel/{hotelA.HotelId}", housekeeperA))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendRawAsync(HttpMethod.Get, $"/api/Housekeeping/hotel/{hotelB.HotelId}", housekeeperA))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
