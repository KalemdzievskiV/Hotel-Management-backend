using System.Net;
using FluentAssertions;
using HotelManagement.Controllers;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Money on a reservation: payments, refunds and cancellations kept in line with the ledger
/// </summary>
public class PaymentsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly TestApi _api;

    public PaymentsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
    }

    private Task<HttpResponseMessage> PayAsync(TestApi.Hotel hotel, int reservationId, decimal amount, string? token = null) =>
        _api.SendAsync(HttpMethod.Post, $"/api/Reservations/{reservationId}/payment", token ?? hotel.AdminToken,
            new RecordPaymentRequest { Amount = amount, PaymentMethod = PaymentMethod.Cash, Reference = $"R-{amount}" });

    private Task<HttpResponseMessage> RefundAsync(TestApi.Hotel hotel, int reservationId, decimal amount) =>
        _api.SendAsync(HttpMethod.Post, $"/api/Reservations/{reservationId}/refund", hotel.AdminToken,
            new RecordRefundRequest { Amount = amount, Reason = "Test" });

    private Task<ReservationDto> GetAsync(TestApi.Hotel hotel, int reservationId) =>
        _api.GetAsync<ReservationDto>($"/api/Reservations/{reservationId}", hotel.AdminToken);

    [Fact]
    public async Task PartialThenFullPayment_SettlesTheBalanceAndKeepsBothInTheLedger()
    {
        var hotel = await _api.CreateHotelAsync();
        var booking = await _api.BookAsync(hotel, nights: 2); // 200

        (await PayAsync(hotel, booking.Id, 50)).StatusCode.Should().Be(HttpStatusCode.OK);
        var partly = await GetAsync(hotel, booking.Id);
        partly.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);
        partly.RemainingAmount.Should().Be(150);

        (await PayAsync(hotel, booking.Id, 150)).StatusCode.Should().Be(HttpStatusCode.OK);
        var paid = await GetAsync(hotel, booking.Id);
        paid.PaymentStatus.Should().Be(PaymentStatus.Paid);
        paid.RemainingAmount.Should().Be(0);
        paid.DepositAmount.Should().Be(200);

        var ledger = await _api.GetAsync<List<PaymentDto>>($"/api/Reservations/{booking.Id}/payments", hotel.AdminToken);
        ledger.Should().HaveCount(2).And.OnlyContain(p => p.Type == PaymentTransactionType.Payment);
        ledger.Select(p => p.Reference).Should().BeEquivalentTo("R-50", "R-150");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(201)]
    public async Task PaymentsOutsideTheBalance_AreRefused(decimal amount)
    {
        var hotel = await _api.CreateHotelAsync();
        var booking = await _api.BookAsync(hotel, nights: 2);

        (await PayAsync(hotel, booking.Id, amount)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetAsync(hotel, booking.Id)).DepositAmount.Should().Be(0);
    }

    [Fact]
    public async Task CancellingAPaidBooking_OwesTheMoneyBackUntilItIsRefunded()
    {
        var hotel = await _api.CreateHotelAsync();
        var booking = await _api.BookAsync(hotel, nights: 1);
        await PayAsync(hotel, booking.Id, 100);

        var cancelled = await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/cancel", hotel.AdminToken,
            new CancelReservationRequest { Reason = "Plans changed" });
        cancelled.Status.Should().Be(ReservationStatus.Cancelled);
        cancelled.PaymentStatus.Should().Be(PaymentStatus.Refunding);

        // No new money on a cancelled booking, and no refunding more than was paid
        (await PayAsync(hotel, booking.Id, 10)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefundAsync(hotel, booking.Id, 150)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await RefundAsync(hotel, booking.Id, 60)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync(hotel, booking.Id)).PaymentStatus.Should().Be(PaymentStatus.Refunding);

        (await RefundAsync(hotel, booking.Id, 40)).StatusCode.Should().Be(HttpStatusCode.OK);
        var refunded = await GetAsync(hotel, booking.Id);
        refunded.PaymentStatus.Should().Be(PaymentStatus.Refunded);
        refunded.DepositAmount.Should().Be(0);
    }

    [Fact]
    public async Task ABookingWithPayments_CannotBeDeletedOnlyCancelled()
    {
        var hotel = await _api.CreateHotelAsync();
        var booking = await _api.BookAsync(hotel);
        await PayAsync(hotel, booking.Id, 20);

        (await _api.SendAsync(HttpMethod.Delete, $"/api/Reservations/{booking.Id}", hotel.AdminToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StaffBookings_AreConfirmedStraightAway_GuestBookingsWaitForApproval()
    {
        var hotel = await _api.CreateHotelAsync();

        var staffBooking = await _api.BookAsync(hotel);
        staffBooking.Status.Should().Be(ReservationStatus.Confirmed);
        staffBooking.ConfirmedAt.Should().NotBeNull();

        var managerToken = await TestAuth.GetTokenAsync(_api.Client, "Manager", hotelId: hotel.HotelId);
        var guest = await _api.CreateGuestAsync(hotel);
        var managerBooking = await _api.PostAsync<ReservationDto>("/api/Reservations", managerToken, new CreateReservationDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            GuestId = guest.Id,
            CheckInDate = DateTime.UtcNow.Date.AddDays(30),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(31),
            NumberOfGuests = 1
        });
        managerBooking.Status.Should().Be(ReservationStatus.Confirmed);

        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        var profile = await _api.GetAsync<GuestDto>("/api/Guests/me", guestToken);
        var guestBooking = await _api.PostAsync<ReservationDto>("/api/Reservations", guestToken, new CreateReservationDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            GuestId = profile.Id,
            CheckInDate = DateTime.UtcNow.Date.AddDays(40),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(41),
            NumberOfGuests = 1
        });
        guestBooking.Status.Should().Be(ReservationStatus.Pending);
    }

    [Fact]
    public async Task ABookingMadeByMistake_CanBeDeletedUntilMoneyOrACheckInIsRecorded()
    {
        var hotel = await _api.CreateHotelAsync();
        var mistake = await _api.BookAsync(hotel);
        (await _api.SendAsync(HttpMethod.Delete, $"/api/Reservations/{mistake.Id}", hotel.AdminToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stay = await _api.WalkInAsync(hotel);
        (await _api.SendAsync(HttpMethod.Delete, $"/api/Reservations/{stay.Id}", hotel.AdminToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CheckedInStays_CannotBeCancelled()
    {
        var hotel = await _api.CreateHotelAsync();
        var stay = await _api.WalkInAsync(hotel);

        (await _api.SendAsync(HttpMethod.Post, $"/api/Reservations/{stay.Id}/cancel", hotel.AdminToken, new CancelReservationRequest { Reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NoShow_FreesTheRoom()
    {
        var hotel = await _api.CreateHotelAsync();
        var booking = await _api.BookAsync(hotel, startsInDays: 3, nights: 1);

        var noShow = await _api.PostAsync<ReservationDto>($"/api/Reservations/{booking.Id}/noshow", hotel.AdminToken);
        noShow.Status.Should().Be(ReservationStatus.NoShow);

        // The same dates can be booked again
        var rebooked = await _api.BookAsync(hotel, startsInDays: 3, nights: 1);
        rebooked.Status.Should().Be(ReservationStatus.Confirmed);
    }

    [Fact]
    public async Task Guests_SeeTheirOwnLedgerButCannotRecordMoney()
    {
        var hotel = await _api.CreateHotelAsync();
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        var profile = await _api.GetAsync<GuestDto>("/api/Guests/me", guestToken);
        var booking = await _api.PostAsync<ReservationDto>("/api/Reservations", guestToken, new CreateReservationDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            GuestId = profile.Id,
            CheckInDate = DateTime.UtcNow.Date.AddDays(20),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(21),
            NumberOfGuests = 1
        });
        await PayAsync(hotel, booking.Id, 30);

        (await PayAsync(hotel, booking.Id, 10, guestToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, $"/api/Reservations/{booking.Id}/refund", guestToken, new RecordRefundRequest { Amount = 30 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ledger = await _api.GetAsync<List<PaymentDto>>($"/api/Reservations/{booking.Id}/payments", guestToken);
        ledger.Should().ContainSingle(p => p.Amount == 30);

        var otherGuestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");
        (await _api.SendAsync(HttpMethod.Get, $"/api/Reservations/{booking.Id}/payments", otherGuestToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OtherHotelsStaff_CannotTouchTheMoney()
    {
        var hotel = await _api.CreateHotelAsync();
        var other = await _api.CreateHotelAsync();
        var booking = await _api.BookAsync(hotel);
        await PayAsync(hotel, booking.Id, 50);

        (await PayAsync(other, booking.Id, 10)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RefundAsync(other, booking.Id, 10)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Get, $"/api/Reservations/{booking.Id}/payments", other.AdminToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetAsync(hotel, booking.Id)).DepositAmount.Should().Be(50);
    }
}
