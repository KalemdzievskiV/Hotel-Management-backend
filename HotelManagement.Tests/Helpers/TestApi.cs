using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;

namespace HotelManagement.Tests.Helpers;

/// <summary>
/// Authenticated calls against the test server, plus a small hotel setup most tests start from
/// </summary>
public class TestApi
{
    public HttpClient Client { get; }

    public TestApi(HttpClient client)
    {
        Client = client;
    }

    public record Hotel(int HotelId, int RoomId, string AdminToken);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
            request.Content = JsonContent.Create(body);
        return await Client.SendAsync(request);
    }

    public async Task<T> SendAsync<T>(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await SendAsync(method, url, token, body);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{method} {url} failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public Task<T> GetAsync<T>(string url, string token) => SendAsync<T>(HttpMethod.Get, url, token);

    public Task<T> PostAsync<T>(string url, string token, object? body = null) => SendAsync<T>(HttpMethod.Post, url, token, body ?? new { });

    /// <summary>
    /// A hotel owned by a fresh Admin, with one double room at 100 a night
    /// </summary>
    public async Task<Hotel> CreateHotelAsync()
    {
        var adminToken = await TestAuth.GetTokenAsync(Client, "Admin");
        var hotel = await PostAsync<HotelDto>("/api/Hotels", adminToken, new HotelDto
        {
            Name = $"Test Hotel {Guid.NewGuid():N}",
            Address = "1 Test St",
            City = "TestCity",
            Country = "TestCountry"
        });
        var room = await PostAsync<RoomDto>("/api/Rooms", adminToken, new RoomDto
        {
            HotelId = hotel.Id,
            RoomNumber = "101",
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        });
        return new Hotel(hotel.Id, room.Id, adminToken);
    }

    public Task<GuestDto> CreateGuestAsync(Hotel hotel, string? email = null) =>
        PostAsync<GuestDto>("/api/Guests", hotel.AdminToken, new GuestDto
        {
            HotelId = hotel.HotelId,
            FirstName = "Walk",
            LastName = "In",
            Email = email ?? $"guest{Guid.NewGuid():N}@test.com",
            PhoneNumber = "+1-555-0100"
        });

    /// <summary>
    /// A walk-in guest checked into the hotel's room tonight for the given number of nights
    /// </summary>
    public Task<ReservationDto> WalkInAsync(Hotel hotel, int nights = 1, decimal deposit = 0, string? email = null) =>
        PostAsync<ReservationDto>("/api/WalkIn/quick-checkin", hotel.AdminToken, new QuickCheckInDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            CheckInDate = DateTime.UtcNow.Date,
            CheckOutDate = DateTime.UtcNow.Date.AddDays(nights),
            DepositAmount = deposit,
            PaymentMethod = PaymentMethod.Cash,
            NewGuest = new QuickGuestDto
            {
                FirstName = "Walk",
                LastName = "In",
                Email = email ?? $"walkin{Guid.NewGuid():N}@test.com",
                PhoneNumber = "+1-555-0100"
            }
        });

    /// <summary>
    /// A pending reservation of the hotel's room starting the given number of days from today
    /// </summary>
    public async Task<ReservationDto> BookAsync(Hotel hotel, int startsInDays = 10, int nights = 2)
    {
        var guest = await CreateGuestAsync(hotel);
        return await PostAsync<ReservationDto>("/api/Reservations", hotel.AdminToken, new CreateReservationDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            GuestId = guest.Id,
            CheckInDate = DateTime.UtcNow.Date.AddDays(startsInDays),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(startsInDays + nights),
            NumberOfGuests = 1
        });
    }

    /// <summary>
    /// Id of a user, looked up by email as a SuperAdmin
    /// </summary>
    public async Task<string> GetUserIdAsync(string email)
    {
        var superAdminToken = await TestAuth.GetTokenAsync(Client, "SuperAdmin");
        var users = await GetAsync<List<UserDto>>($"/api/Users/search?searchTerm={Uri.EscapeDataString(email)}", superAdminToken);
        return users.Single().Id;
    }
}
