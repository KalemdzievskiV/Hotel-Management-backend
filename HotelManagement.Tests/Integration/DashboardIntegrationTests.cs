using System.Net;
using FluentAssertions;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// The front desk's Today screen (GET /Dashboard/today, /Dashboard/trend) and the paged,
/// hotel-scoped booking lists (GET /Reservations/search, today/check-ins?hotelId=)
/// </summary>
public class DashboardIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly TestApi _api;
    private static DateTime Today => DateTime.UtcNow.Date;
    private static string Day(DateTime date) => date.ToString("yyyy-MM-dd");

    public DashboardIntegrationTests(CustomWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
    }

    private Task<RoomDto> AddRoomAsync(TestApi.Hotel hotel, string number) =>
        _api.PostAsync<RoomDto>("/api/Rooms", hotel.AdminToken, new RoomDto
        {
            HotelId = hotel.HotelId,
            RoomNumber = number,
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 100
        });

    /// <summary>A booking made by the hotel (so it's confirmed), for a new guest</summary>
    private async Task<ReservationDto> BookAsync(TestApi.Hotel hotel, int roomId, DateTime checkIn, int nights, string lastName = "Guest")
    {
        var guest = await _api.PostAsync<GuestDto>("/api/Guests", hotel.AdminToken, new GuestDto
        {
            HotelId = hotel.HotelId,
            FirstName = "Desk",
            LastName = lastName,
            Email = $"desk{Guid.NewGuid():N}@test.com",
            PhoneNumber = "+1-555-0100"
        });
        return await _api.PostAsync<ReservationDto>("/api/Reservations", hotel.AdminToken, new CreateReservationDto
        {
            HotelId = hotel.HotelId,
            RoomId = roomId,
            GuestId = guest.Id,
            CheckInDate = checkIn,
            CheckOutDate = checkIn.AddDays(nights),
            NumberOfGuests = 1
        });
    }

    private Task<TodayDashboardDto> TodayAsync(TestApi.Hotel hotel, DateTime? date = null) =>
        _api.GetAsync<TodayDashboardDto>($"/api/Dashboard/today?hotelId={hotel.HotelId}&date={Day(date ?? Today)}", hotel.AdminToken);

    #region Today

    [Fact]
    public async Task Today_CountsInHouseOccupancyRevenueAndPendingApprovals()
    {
        var hotel = await _api.CreateHotelAsync();
        var second = await AddRoomAsync(hotel, "102");
        await _api.WalkInAsync(hotel, nights: 2, deposit: 30);

        // An online booking for later, waiting for the hotel
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        await _api.PostAsync<ReservationDto>("/api/Reservations", guestToken, new CreateReservationDto
        {
            HotelId = hotel.HotelId,
            RoomId = second.Id,
            CheckInDate = Today.AddDays(5),
            CheckOutDate = Today.AddDays(7),
            NumberOfGuests = 1
        });

        var today = await TodayAsync(hotel);

        today.TotalRooms.Should().Be(2);
        today.OccupiedRooms.Should().Be(1);
        today.OccupancyPercent.Should().Be(50);
        today.InHouse.Should().Be(1);
        today.Arrivals.Should().Be(1);
        today.ArrivalsCheckedIn.Should().Be(1);
        today.Departures.Should().Be(0);
        today.RevenueToday.Should().Be(30);
        today.PendingApprovals.Should().Be(1);
        today.RoomsByStatus.Should().ContainEquivalentOf(new RoomStatusCountDto { Status = RoomStatus.Occupied, Count = 1 });
        today.Attention.Should().ContainSingle(a => a.Kind == AttentionKinds.PendingApproval);
    }

    [Fact]
    public async Task Today_FlagsUnpaidDeparturesAndOverdueArrivals()
    {
        var hotel = await _api.CreateHotelAsync();
        var second = await AddRoomAsync(hotel, "102");

        // Arrived yesterday, leaving today, nothing paid
        var leaving = await BookAsync(hotel, hotel.RoomId, Today.AddDays(-1), nights: 1, lastName: "Leaving");
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{leaving.Id}/checkin", hotel.AdminToken);

        // Due yesterday and never checked in
        var overdue = await BookAsync(hotel, second.Id, Today.AddDays(-1), nights: 3, lastName: "Late");

        var today = await TodayAsync(hotel);

        today.Departures.Should().Be(1);
        today.Attention.Should().ContainSingle(a => a.Kind == AttentionKinds.UnpaidDeparture && a.ReservationId == leaving.Id)
            .Which.Amount.Should().Be(100);
        today.Attention.Should().ContainSingle(a => a.Kind == AttentionKinds.OverdueArrival && a.ReservationId == overdue.Id);
        // Overdue comes first: the room is being held for someone who isn't here
        today.Attention.First().Kind.Should().Be(AttentionKinds.OverdueArrival);
    }

    [Fact]
    public async Task Today_UsesTheDateTheHotelSends()
    {
        var hotel = await _api.CreateHotelAsync();
        await BookAsync(hotel, hotel.RoomId, Today.AddDays(1), nights: 2);

        (await TodayAsync(hotel)).Arrivals.Should().Be(0);
        (await TodayAsync(hotel, Today.AddDays(1))).Arrivals.Should().Be(1);
    }

    [Fact]
    public async Task Today_ForAnotherOwnersHotel_IsForbidden()
    {
        var hotel = await _api.CreateHotelAsync();
        var other = await _api.CreateHotelAsync();

        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Dashboard/today?hotelId={hotel.HotelId}", other.AdminToken);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        (await _api.SendAsync(HttpMethod.Get, $"/api/Dashboard/today?hotelId={hotel.HotelId}", guestToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Trend

    [Fact]
    public async Task Trend_ReturnsOneRowPerDay_WithTodaysRevenueAndOccupancy()
    {
        var hotel = await _api.CreateHotelAsync();
        await _api.WalkInAsync(hotel, nights: 1, deposit: 40);

        var trend = await _api.GetAsync<List<DailyTrendDto>>(
            $"/api/Dashboard/trend?hotelId={hotel.HotelId}&date={Day(Today)}&days=7", hotel.AdminToken);

        trend.Should().HaveCount(7);
        trend.First().Date.Should().Be(Today.AddDays(-6));
        trend.Last().Date.Should().Be(Today);
        trend.Last().Revenue.Should().Be(40);
        trend.Last().OccupiedRooms.Should().Be(1);
        trend.Last().OccupancyPercent.Should().Be(100);
        trend.Take(6).Should().OnlyContain(d => d.Revenue == 0 && d.OccupiedRooms == 0);
    }

    [Fact]
    public async Task Trend_WithTooManyDays_IsRejected()
    {
        var hotel = await _api.CreateHotelAsync();
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Dashboard/trend?hotelId={hotel.HotelId}&days=90", hotel.AdminToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Search

    [Fact]
    public async Task Search_FiltersBySegmentAndText_AndPages()
    {
        var hotel = await _api.CreateHotelAsync();
        var second = await AddRoomAsync(hotel, "102");
        await _api.WalkInAsync(hotel, nights: 2);
        await BookAsync(hotel, second.Id, Today.AddDays(3), nights: 1, lastName: "Novak");
        await BookAsync(hotel, second.Id, Today.AddDays(10), nights: 1, lastName: "Horvat");

        async Task<PagedResult<ReservationDto>> Search(string query) =>
            await _api.GetAsync<PagedResult<ReservationDto>>(
                $"/api/Reservations/search?hotelId={hotel.HotelId}&date={Day(Today)}&{query}", hotel.AdminToken);

        (await Search($"segment={ReservationSegments.InHouse}")).TotalCount.Should().Be(1);
        (await Search($"segment={ReservationSegments.Arrivals}")).TotalCount.Should().Be(1);
        (await Search($"segment={ReservationSegments.Upcoming}")).Items
            .Select(r => r.GuestName).Should().Equal("Desk Novak", "Desk Horvat");
        (await Search("segment=all&q=horv")).Items.Should().ContainSingle(r => r.GuestName == "Desk Horvat");

        var firstPage = await Search("segment=all&pageSize=2&page=1");
        firstPage.TotalCount.Should().Be(3);
        firstPage.Items.Should().HaveCount(2);
        firstPage.HasMore.Should().BeTrue();
        (await Search("segment=all&pageSize=2&page=2")).Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_WithUnknownSegment_IsRejected()
    {
        var hotel = await _api.CreateHotelAsync();
        var response = await _api.SendAsync(HttpMethod.Get, $"/api/Reservations/search?hotelId={hotel.HotelId}&segment=everything", hotel.AdminToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TodaysCheckIns_WithHotelId_OnlyReturnsThatHotel()
    {
        // A SuperAdmin sees every hotel, so without hotelId both walk-ins come back
        var first = await _api.CreateHotelAsync();
        var second = await _api.CreateHotelAsync();
        var mine = await _api.WalkInAsync(first);
        await _api.WalkInAsync(second);
        var superAdmin = await TestAuth.GetTokenAsync(_api.Client, "SuperAdmin");

        var scoped = await _api.GetAsync<List<ReservationDto>>(
            $"/api/Reservations/today/check-ins?hotelId={first.HotelId}&date={Day(Today)}", superAdmin);

        scoped.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
        (await _api.SendAsync(HttpMethod.Get, $"/api/Reservations/today/check-ins?hotelId={first.HotelId}", second.AdminToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion
}
