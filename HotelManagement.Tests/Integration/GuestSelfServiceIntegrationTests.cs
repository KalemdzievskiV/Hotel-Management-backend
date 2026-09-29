using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// What a guest does for themselves in the mobile app: edit their profile, change their
/// password, book and cancel their own stays
/// </summary>
public class GuestSelfServiceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Test123";

    private readonly HttpClient _client;
    private readonly TestApi _api;

    public GuestSelfServiceIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _api = new TestApi(_client);
    }

    private async Task<(AuthResponseDto Auth, string Email)> SignInGuestAsync()
    {
        var email = $"self{Guid.NewGuid():N}@test.com";
        await TestAuth.GetTokenAsync(_client, "Guest", email);
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequestDto { Email = email, Password = Password });
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<AuthResponseDto>())!, email);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/Auth/login", new LoginRequestDto { Email = email, Password = password });

    private static CreateReservationDto Booking(TestApi.Hotel hotel, DateTime checkIn, int nights = 2) => new()
    {
        HotelId = hotel.HotelId,
        RoomId = hotel.RoomId,
        BookingType = BookingType.Daily,
        CheckInDate = checkIn.Date,
        CheckOutDate = checkIn.Date.AddDays(nights),
        NumberOfGuests = 1
    };

    #region Profile

    [Fact]
    public async Task UpdateMyProfile_ChangesGuestProfileAndAccountName()
    {
        var (auth, email) = await SignInGuestAsync();

        var updated = await _api.SendAsync<GuestDto>(HttpMethod.Put, "/api/Guests/me", auth.Token, new UpdateMyProfileDto
        {
            FirstName = "Ana",
            LastName = "Petrovska",
            PhoneNumber = "+389 70 123 456",
            City = "Skopje",
            Country = "North Macedonia"
        });

        updated.FirstName.Should().Be("Ana");
        updated.LastName.Should().Be("Petrovska");
        updated.PhoneNumber.Should().Be("+389 70 123 456");
        updated.City.Should().Be("Skopje");
        updated.Email.Should().Be(email, "the email is the sign-in name and can't be changed here");

        var profile = await _api.GetAsync<GuestDto>("/api/Guests/me", auth.Token);
        profile.Id.Should().Be(updated.Id);
        profile.Country.Should().Be("North Macedonia");

        var login = await (await LoginAsync(email, Password)).Content.ReadFromJsonAsync<AuthResponseDto>();
        login!.FullName.Should().Be("Ana Petrovska");
    }

    [Fact]
    public async Task UpdateMyProfile_WithInvalidName_IsRejected()
    {
        var (auth, _) = await SignInGuestAsync();

        var response = await _api.SendAsync(HttpMethod.Put, "/api/Guests/me", auth.Token, new UpdateMyProfileDto
        {
            FirstName = "A",
            LastName = "Petrovska"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMyProfile_WithoutAuth_IsUnauthorized()
    {
        var response = await _client.PutAsJsonAsync("/api/Guests/me", new UpdateMyProfileDto { FirstName = "Ana", LastName = "Petrovska" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Change password

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_IsRejected()
    {
        var (auth, email) = await SignInGuestAsync();

        var response = await _api.SendAsync(HttpMethod.Post, "/api/Auth/change-password", auth.Token, new ChangePasswordRequestDto
        {
            CurrentPassword = "Wrong123",
            NewPassword = "Newpass123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("current password is incorrect");
        (await LoginAsync(email, Password)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithWeakPassword_IsRejected()
    {
        var (auth, _) = await SignInGuestAsync();

        var response = await _api.SendAsync(HttpMethod.Post, "/api/Auth/change-password", auth.Token, new ChangePasswordRequestDto
        {
            CurrentPassword = Password,
            NewPassword = "alllowercase"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_SwitchesPassword_KeepsThisDeviceAndEndsOtherSignIns()
    {
        var (auth, email) = await SignInGuestAsync();

        var changed = await _api.PostAsync<AuthResponseDto>("/api/Auth/change-password", auth.Token, new ChangePasswordRequestDto
        {
            CurrentPassword = Password,
            NewPassword = "Newpass123"
        });

        changed.Token.Should().NotBeNullOrEmpty();
        changed.RefreshToken.Should().NotBe(auth.RefreshToken);

        (await LoginAsync(email, Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(email, "Newpass123")).StatusCode.Should().Be(HttpStatusCode.OK);

        // The sign-in from before the change can't be renewed; the one it returned can
        var oldRefresh = await _client.PostAsJsonAsync("/api/Auth/refresh", new RefreshTokenRequestDto { RefreshToken = auth.RefreshToken });
        oldRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var newRefresh = await _client.PostAsJsonAsync("/api/Auth/refresh", new RefreshTokenRequestDto { RefreshToken = changed.RefreshToken });
        newRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuth_IsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/change-password", new ChangePasswordRequestDto
        {
            CurrentPassword = Password,
            NewPassword = "Newpass123"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Booking and cancelling

    [Fact]
    public async Task Guest_BooksAvailableRoom_ThenCancelsIt()
    {
        var hotel = await _api.CreateHotelAsync();
        var (auth, _) = await SignInGuestAsync();
        var checkIn = DateTime.UtcNow.Date.AddDays(10);

        var available = await _api.GetAsync<AvailableRoomsResponse>(
            $"/api/Reservations/available-rooms?hotelId={hotel.HotelId}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(2):yyyy-MM-dd}",
            auth.Token);
        available.Rooms.Should().ContainSingle(r => r.Id == hotel.RoomId);

        var booked = await _api.PostAsync<ReservationDto>("/api/Reservations", auth.Token, Booking(hotel, checkIn));
        booked.Status.Should().Be(ReservationStatus.Pending, "a guest's online booking waits for the hotel to approve it");
        booked.TotalAmount.Should().Be(200);
        booked.RemainingAmount.Should().Be(200);

        var cancelled = await _api.PostAsync<ReservationDto>($"/api/Reservations/{booked.Id}/cancel", auth.Token, new { reason = "Plans changed" });
        cancelled.Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Guest_CannotBookInThePast()
    {
        var hotel = await _api.CreateHotelAsync();
        var (auth, _) = await SignInGuestAsync();

        var response = await _api.SendAsync(HttpMethod.Post, "/api/Reservations", auth.Token,
            Booking(hotel, DateTime.UtcNow.Date.AddDays(-5)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Guest_CannotCancelStayThatHasStarted_ButHotelCan()
    {
        var hotel = await _api.CreateHotelAsync();
        var (auth, _) = await SignInGuestAsync();
        var profile = await _api.GetAsync<GuestDto>("/api/Guests/me", auth.Token);

        // The hotel booked it for the guest, starting two days ago; the guest hasn't turned up
        var booking = Booking(hotel, DateTime.UtcNow.Date.AddDays(-2), nights: 4);
        booking.GuestId = profile.Id;
        var reservation = await _api.PostAsync<ReservationDto>("/api/Reservations", hotel.AdminToken, booking);
        reservation.Status.Should().Be(ReservationStatus.Confirmed);

        var byGuest = await _api.SendAsync(HttpMethod.Post, $"/api/Reservations/{reservation.Id}/cancel", auth.Token, new { reason = "Too late" });
        byGuest.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await byGuest.Content.ReadAsStringAsync()).Should().Contain("already started");

        var byHotel = await _api.PostAsync<ReservationDto>($"/api/Reservations/{reservation.Id}/cancel", hotel.AdminToken, new { reason = "Guest called" });
        byHotel.Status.Should().Be(ReservationStatus.Cancelled);
    }

    #endregion

    private record AvailableRoomsResponse(int HotelId, int TotalAvailable, List<RoomDto> Rooms);
}
