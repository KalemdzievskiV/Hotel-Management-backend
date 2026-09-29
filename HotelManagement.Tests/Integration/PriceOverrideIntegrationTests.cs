using System.Net;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Staff can charge a different price than the room's default when booking, higher or lower
/// </summary>
public class PriceOverrideIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly TestApi _api;

    public PriceOverrideIntegrationTests(CustomWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
    }

    private async Task<CreateReservationDto> TwoNightsAsync(TestApi.Hotel hotel, decimal? price, decimal deposit = 0)
    {
        var guest = await _api.CreateGuestAsync(hotel);
        return new CreateReservationDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            GuestId = guest.Id,
            CheckInDate = DateTime.UtcNow.Date.AddDays(10),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(12),
            NumberOfGuests = 1,
            DepositAmount = deposit,
            OverridePrice = price
        };
    }

    [Fact]
    public async Task AdminCanChargeMoreThanTheDefault()
    {
        var hotel = await _api.CreateHotelAsync(); // 100 a night

        var booking = await _api.PostAsync<ReservationDto>("/api/Reservations", hotel.AdminToken,
            await TwoNightsAsync(hotel, price: 260, deposit: 250));

        booking.TotalAmount.Should().Be(260);
        booking.DiscountAmount.Should().Be(-60, "a surcharge is a negative discount so the default stays visible");
        booking.DiscountReason.Should().Contain("200.00").And.Contain("260.00");
        booking.RemainingAmount.Should().Be(10);
    }

    [Fact]
    public async Task AssignedManagerCanChargeLessThanTheDefault()
    {
        var hotel = await _api.CreateHotelAsync();
        var managerToken = await TestAuth.GetTokenAsync(_api.Client, "Manager", hotelId: hotel.HotelId);

        var dto = await TwoNightsAsync(hotel, price: 150);
        dto.OverridePriceReason = "Regular customer";
        var booking = await _api.PostAsync<ReservationDto>("/api/Reservations", managerToken, dto);

        booking.TotalAmount.Should().Be(150);
        booking.DiscountAmount.Should().Be(50);
        booking.DiscountReason.Should().Be("Regular customer");
    }

    [Fact]
    public async Task WithoutAPrice_TheDefaultIsCharged()
    {
        var hotel = await _api.CreateHotelAsync();

        var booking = await _api.PostAsync<ReservationDto>("/api/Reservations", hotel.AdminToken, await TwoNightsAsync(hotel, price: null));

        booking.TotalAmount.Should().Be(200);
        booking.DiscountAmount.Should().Be(0);
    }

    [Fact]
    public async Task DepositAboveTheChangedPrice_IsRefused()
    {
        var hotel = await _api.CreateHotelAsync();

        var response = await _api.SendAsync(HttpMethod.Post, "/api/Reservations", hotel.AdminToken,
            await TwoNightsAsync(hotel, price: 120, deposit: 150));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WalkInCanAlsoChargeMoreThanTheDefault()
    {
        var hotel = await _api.CreateHotelAsync();

        var stay = await _api.PostAsync<ReservationDto>("/api/WalkIn/quick-checkin", hotel.AdminToken, new QuickCheckInDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            CheckInDate = DateTime.UtcNow.Date,
            CheckOutDate = DateTime.UtcNow.Date.AddDays(1),
            OverridePrice = 130,
            NewGuest = new QuickGuestDto { FirstName = "Late" }
        });

        stay.TotalAmount.Should().Be(130);
        stay.DiscountAmount.Should().Be(-30);
    }
}
