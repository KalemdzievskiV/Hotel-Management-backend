using System.Net;
using FluentAssertions;
using HotelManagement.Controllers;
using HotelManagement.Models.DTOs;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Reports only cover the caller's hotels and include what happened today
/// </summary>
public class ReportsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly TestApi _api;

    public ReportsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
    }

    private static string Today => DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

    /// <summary>
    /// A hotel with one finished, fully paid 100 stay that started today
    /// </summary>
    private async Task<TestApi.Hotel> CreateHotelWithFinishedStayAsync()
    {
        var hotel = await _api.CreateHotelAsync();
        var stay = await _api.WalkInAsync(hotel, deposit: 100);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{stay.Id}/checkout", hotel.AdminToken);
        return hotel;
    }

    [Fact]
    public async Task Revenue_OnlyCountsTheCallersHotels()
    {
        var hotelA = await CreateHotelWithFinishedStayAsync();
        var hotelB = await CreateHotelWithFinishedStayAsync();
        await _api.WalkInAsync(hotelB); // an open stay at B doesn't count either way

        var daily = await _api.GetAsync<List<DailyRevenueDto>>($"/api/Reports/revenue/daily?startDate={Today}&endDate={Today}", hotelA.AdminToken);
        daily.Should().ContainSingle().Which.TotalRevenue.Should().Be(100);

        var reconciliation = await _api.GetAsync<List<PaymentReconciliationDto>>($"/api/Reports/payments/reconciliation?startDate={Today}&endDate={Today}", hotelA.AdminToken);
        reconciliation.Should().ContainSingle().Which.CashRevenue.Should().Be(100);

        var outstanding = await _api.GetAsync<List<OutstandingPaymentDto>>("/api/Reports/payments/outstanding", hotelA.AdminToken);
        outstanding.Should().BeEmpty();
        (await _api.GetAsync<List<OutstandingPaymentDto>>("/api/Reports/payments/outstanding", hotelB.AdminToken))
            .Should().ContainSingle().Which.RemainingAmount.Should().Be(100);
    }

    [Fact]
    public async Task ReservationStats_OnlyCountTheCallersHotels()
    {
        var hotelA = await CreateHotelWithFinishedStayAsync();
        await CreateHotelWithFinishedStayAsync();

        var count = await _api.GetAsync<Dictionary<string, int>>("/api/Reservations/stats/count", hotelA.AdminToken);
        count["totalReservations"].Should().Be(1);
        var revenue = await _api.GetAsync<Dictionary<string, decimal>>("/api/Reservations/stats/revenue", hotelA.AdminToken);
        revenue["totalRevenue"].Should().Be(100);
    }

    [Fact]
    public async Task CancellationsReport_IncludesTodaysCancellations()
    {
        var hotel = await _api.CreateHotelAsync();
        var booking = await _api.BookAsync(hotel, nights: 1);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/cancel", hotel.AdminToken,
            new CancelReservationRequest { Reason = "Plans changed" });

        // As the reports page asks for it: a date range ending today, and the default range
        var byRange = await _api.GetAsync<List<CancellationReportDto>>($"/api/Reports/cancellations?startDate={Today}&endDate={Today}", hotel.AdminToken);
        byRange.Should().ContainSingle().Which.MostCommonReason.Should().Be("Plans changed");

        var byDefault = await _api.GetAsync<List<CancellationReportDto>>("/api/Reports/cancellations", hotel.AdminToken);
        byDefault.Should().ContainSingle().Which.LostRevenue.Should().Be(100);
    }

    [Fact]
    public async Task NoShowsReport_ListsTheCallersNoShows()
    {
        var hotel = await _api.CreateHotelAsync();
        var other = await _api.CreateHotelAsync();
        var booking = await _api.BookAsync(hotel, startsInDays: 0, nights: 1);
        await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/noshow", hotel.AdminToken);

        (await _api.GetAsync<List<NoShowReportDto>>($"/api/Reports/noshows?startDate={Today}&endDate={Today}", hotel.AdminToken))
            .Should().ContainSingle(n => n.ReservationId == booking.Id);
        (await _api.GetAsync<List<NoShowReportDto>>($"/api/Reports/noshows?startDate={Today}&endDate={Today}", other.AdminToken))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("Housekeeper")]
    [InlineData("Guest")]
    public async Task Reports_AreForManagementOnly(string role)
    {
        var token = await TestAuth.GetTokenAsync(_api.Client, role);

        (await _api.SendAsync(HttpMethod.Get, "/api/Reports/revenue/daily", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Get, "/api/Reservations/stats/revenue", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
