using System.Net;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Front-desk walk-ins: booking, paying and checking in in one step, and settling up at checkout
/// </summary>
public class WalkInIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly TestApi _api;

    public WalkInIntegrationTests(CustomWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
    }

    [Fact]
    public async Task QuickCheckIn_WithDiscountAndDeposit_ChecksInWithTheRightBalance()
    {
        var hotel = await _api.CreateHotelAsync();

        var stay = await _api.PostAsync<ReservationDto>("/api/WalkIn/quick-checkin", hotel.AdminToken, new QuickCheckInDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            CheckInDate = DateTime.UtcNow.Date,
            CheckOutDate = DateTime.UtcNow.Date.AddDays(2),
            DiscountAmount = 20,
            DiscountReason = "Regular",
            DepositAmount = 50,
            PaymentMethod = PaymentMethod.CreditCard,
            NewGuest = new QuickGuestDto { FirstName = "Wanda", LastName = "Walker", Email = $"w{Guid.NewGuid():N}@test.com", PhoneNumber = "+1-555-0101" }
        });

        stay.Status.Should().Be(ReservationStatus.CheckedIn);
        stay.TotalAmount.Should().Be(180);
        stay.DepositAmount.Should().Be(50);
        stay.RemainingAmount.Should().Be(130);
        stay.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);
        stay.GuestName.Should().Be("Wanda Walker");

        var room = await _api.GetAsync<RoomDto>($"/api/Rooms/{hotel.RoomId}", hotel.AdminToken);
        room.Status.Should().Be(RoomStatus.Occupied);
    }

    [Fact]
    public async Task QuickCheckIn_DepositAboveTotal_IsRefused()
    {
        var hotel = await _api.CreateHotelAsync();

        var response = await _api.SendAsync(HttpMethod.Post, "/api/WalkIn/quick-checkin", hotel.AdminToken, new QuickCheckInDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            CheckInDate = DateTime.UtcNow.Date,
            CheckOutDate = DateTime.UtcNow.Date.AddDays(1),
            DepositAmount = 500,
            NewGuest = new QuickGuestDto { FirstName = "Too", LastName = "Much", Email = $"t{Guid.NewGuid():N}@test.com", PhoneNumber = "+1-555-0102" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OccupiedRoom_IsNotOfferedForTonight()
    {
        var hotel = await _api.CreateHotelAsync();
        (await _api.GetAsync<List<RoomDto>>($"/api/WalkIn/available-rooms/{hotel.HotelId}", hotel.AdminToken))
            .Should().ContainSingle(r => r.Id == hotel.RoomId);

        await _api.WalkInAsync(hotel);

        (await _api.GetAsync<List<RoomDto>>($"/api/WalkIn/available-rooms/{hotel.HotelId}", hotel.AdminToken))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ExpressCheckOut_AddsChargesTakesFinalPaymentAndSendsRoomToCleaning()
    {
        var hotel = await _api.CreateHotelAsync();
        var stay = await _api.WalkInAsync(hotel, nights: 1, deposit: 40);

        var done = await _api.PostAsync<ReservationDto>($"/api/WalkIn/express-checkout/{stay.Id}", hotel.AdminToken, new ExpressCheckOutDto
        {
            ExtraCharges = 15,
            ExtraChargesNotes = "Minibar",
            FinalPayment = 75,
            PaymentMethod = PaymentMethod.CreditCard
        });

        done.Status.Should().Be(ReservationStatus.CheckedOut);
        done.TotalAmount.Should().Be(115);
        done.RemainingAmount.Should().Be(0);
        done.PaymentStatus.Should().Be(PaymentStatus.Paid);

        var payments = await _api.GetAsync<List<PaymentDto>>($"/api/Reservations/{stay.Id}/payments", hotel.AdminToken);
        payments.Select(p => p.Amount).Should().BeEquivalentTo(new[] { 40m, 75m });

        var room = await _api.GetAsync<RoomDto>($"/api/Rooms/{hotel.RoomId}", hotel.AdminToken);
        room.Status.Should().Be(RoomStatus.Cleaning);
    }

    [Fact]
    public async Task ExpressCheckOut_OverpayingIsRefusedAndTheGuestStaysCheckedIn()
    {
        var hotel = await _api.CreateHotelAsync();
        var stay = await _api.WalkInAsync(hotel);

        var response = await _api.SendAsync(HttpMethod.Post, $"/api/WalkIn/express-checkout/{stay.Id}", hotel.AdminToken,
            new ExpressCheckOutDto { FinalPayment = 1000 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reservation = await _api.GetAsync<ReservationDto>($"/api/Reservations/{stay.Id}", hotel.AdminToken);
        reservation.Status.Should().Be(ReservationStatus.CheckedIn);
    }

    [Fact]
    public async Task ExpressCheckOut_OfAReservationNotCheckedIn_IsRefused()
    {
        var hotel = await _api.CreateHotelAsync();
        var booking = await _api.BookAsync(hotel);

        var response = await _api.SendAsync(HttpMethod.Post, $"/api/WalkIn/express-checkout/{booking.Id}", hotel.AdminToken, new ExpressCheckOutDto());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OtherHotelsStaff_CannotWalkInOrCheckOutHere()
    {
        var hotel = await _api.CreateHotelAsync();
        var other = await _api.CreateHotelAsync();
        var stay = await _api.WalkInAsync(hotel);

        (await _api.SendAsync(HttpMethod.Post, $"/api/WalkIn/express-checkout/{stay.Id}", other.AdminToken, new ExpressCheckOutDto()))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Get, $"/api/WalkIn/available-rooms/{hotel.HotelId}", other.AdminToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, "/api/WalkIn/quick-checkin", other.AdminToken, new QuickCheckInDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            ExistingGuestId = stay.GuestId,
            CheckInDate = DateTime.UtcNow.Date.AddDays(5),
            CheckOutDate = DateTime.UtcNow.Date.AddDays(6)
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GuestIntelligence_OnlyCountsStaysAtTheCallersHotels()
    {
        var hotelA = await _api.CreateHotelAsync();
        var hotelB = await _api.CreateHotelAsync();
        var email = $"traveller{Guid.NewGuid():N}@test.com";

        // A finished stay at hotel A, then the same person walks into hotel B
        var stayA = await _api.WalkInAsync(hotelA, email: email);
        await _api.PostAsync<ReservationDto>($"/api/WalkIn/express-checkout/{stayA.Id}", hotelA.AdminToken,
            new ExpressCheckOutDto { FinalPayment = 100 });
        var stayB = await _api.WalkInAsync(hotelB, email: email);
        stayB.GuestId.Should().Be(stayA.GuestId);

        var seenByA = await _api.GetAsync<GuestIntelligenceDto>($"/api/WalkIn/guest-intelligence/{stayA.GuestId}", hotelA.AdminToken);
        seenByA.TotalStays.Should().Be(1);
        seenByA.TotalSpent.Should().Be(100);

        var seenByB = await _api.GetAsync<GuestIntelligenceDto>($"/api/WalkIn/guest-intelligence/{stayA.GuestId}", hotelB.AdminToken);
        seenByB.TotalStays.Should().Be(0);
        seenByB.TotalSpent.Should().Be(0);
        seenByB.RecentStays.Should().BeEmpty();
        seenByB.LastStayDate.Should().BeNull();
        seenByB.HasOutstandingPayments.Should().BeTrue(); // tonight's stay at B isn't paid yet
    }

    [Fact]
    public async Task GuestFlags_ManagersCanMarkVipButOnlyAdminsCanBlacklist()
    {
        var hotel = await _api.CreateHotelAsync();
        var guest = await _api.CreateGuestAsync(hotel);
        var managerToken = await TestAuth.GetTokenAsync(_api.Client, "Manager", hotelId: hotel.HotelId);

        (await _api.SendAsync(HttpMethod.Patch, $"/api/WalkIn/guest-flags/{guest.Id}", managerToken, new UpdateGuestFlagsDto { IsVIP = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _api.SendAsync(HttpMethod.Patch, $"/api/WalkIn/guest-flags/{guest.Id}", managerToken, new UpdateGuestFlagsDto { IsBlacklisted = true, BlacklistReason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Patch, $"/api/WalkIn/guest-flags/{guest.Id}", hotel.AdminToken, new UpdateGuestFlagsDto { IsBlacklisted = true, BlacklistReason = "Damage" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var flagged = await _api.GetAsync<GuestDto>($"/api/Guests/{guest.Id}", hotel.AdminToken);
        flagged.IsVIP.Should().BeTrue();
        flagged.IsBlacklisted.Should().BeTrue();

        // A blacklisted guest can't be walked in
        (await _api.SendAsync(HttpMethod.Post, "/api/WalkIn/quick-checkin", hotel.AdminToken, new QuickCheckInDto
        {
            HotelId = hotel.HotelId,
            RoomId = hotel.RoomId,
            ExistingGuestId = guest.Id,
            CheckInDate = DateTime.UtcNow.Date,
            CheckOutDate = DateTime.UtcNow.Date.AddDays(1)
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
