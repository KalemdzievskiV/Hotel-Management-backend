using AutoMapper;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Infrastructure.Mapping;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Xunit;

namespace HotelManagement.Tests.Services;

/// <summary>
/// Booking rules as the UI actually uses them: overnight stays are sent as dates only
/// (midnight), and money only moves through the payments ledger.
/// </summary>
public class ReservationRulesTests
{
    private readonly ApplicationDbContext _context;
    private readonly ReservationService _service;
    private readonly DateTime _day = DateTime.UtcNow.Date.AddDays(10);

    public ReservationRulesTests()
    {
        _context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "staff-id") }))
        });

        _service = new ReservationService(
            _context,
            new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfile>()).CreateMapper(),
            httpContextAccessor.Object);

        // Hotel defaults: check-in 14:00, check-out 11:00, 3h cleaning buffer
        _context.Users.Add(new ApplicationUser { Id = "staff-id", UserName = "staff", FirstName = "Staff", LastName = "User" });
        _context.Hotels.Add(new Hotel { Id = 1, Name = "Hotel", OwnerId = "staff-id", Address = "A", City = "C", Country = "X" });
        _context.Rooms.Add(new Room
        {
            Id = 1, HotelId = 1, RoomNumber = "101", Capacity = 2, PricePerNight = 100,
            AllowsShortStay = true, ShortStayHourlyRate = 20, MinimumShortStayHours = 1, MaximumShortStayHours = 12
        });
        _context.Guests.AddRange(
            new Guest { Id = 1, FirstName = "Ann", LastName = "One", Email = "ann@test.com", PhoneNumber = "1" },
            new Guest { Id = 2, FirstName = "Bob", LastName = "Two", Email = "bob@test.com", PhoneNumber = "2" },
            new Guest { Id = 3, FirstName = "Eve", LastName = "Banned", Email = "eve@test.com", PhoneNumber = "3", IsBlacklisted = true });
        _context.SaveChanges();
    }

    private Task<ReservationDto> BookNightsAsync(int guestId, DateTime checkIn, int nights, decimal deposit = 0) =>
        _service.CreateReservationAsync(new CreateReservationDto
        {
            HotelId = 1, RoomId = 1, GuestId = guestId,
            BookingType = BookingType.Daily,
            CheckInDate = checkIn, CheckOutDate = checkIn.AddDays(nights),
            NumberOfGuests = 1, DepositAmount = deposit, PaymentMethod = PaymentMethod.Cash
        });

    #region Availability

    [Fact]
    public async Task BackToBackOvernightStays_OnDatesOnly_AreAllowed()
    {
        // Ann leaves on day+2 at 11:00, Bob arrives on day+2 at 14:00
        await BookNightsAsync(1, _day, 2);

        var second = await BookNightsAsync(2, _day.AddDays(2), 1);

        Assert.Equal(ReservationStatus.Pending, second.Status);
    }

    [Fact]
    public async Task BackToBackOvernightStays_InReverseOrder_AreAllowed()
    {
        await BookNightsAsync(1, _day.AddDays(2), 1);

        var earlier = await BookNightsAsync(2, _day, 2);

        Assert.Equal(ReservationStatus.Pending, earlier.Status);
    }

    [Fact]
    public async Task OverlappingOvernightStays_AreRejected()
    {
        await BookNightsAsync(1, _day, 2);

        await Assert.ThrowsAsync<BusinessRuleException>(() => BookNightsAsync(2, _day.AddDays(1), 2));
    }

    [Fact]
    public async Task ShortStay_OnCheckoutMorning_BeforeGuestLeaves_IsRejected()
    {
        // Previously missed: the overnight guest is still in the room until 11:00
        await BookNightsAsync(1, _day, 1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateReservationAsync(new CreateReservationDto
        {
            HotelId = 1, RoomId = 1, GuestId = 2, BookingType = BookingType.ShortStay,
            CheckInDate = _day.AddDays(1).AddHours(8), CheckOutDate = _day.AddDays(1).AddHours(10),
            NumberOfGuests = 1
        }));
        Assert.Contains("booked overnight", ex.Message);
    }

    [Fact]
    public async Task ShortStay_OnArrivalDayMorning_BeforeCheckIn_IsAllowed()
    {
        await BookNightsAsync(1, _day, 1); // arrives at 14:00

        var shortStay = await _service.CreateReservationAsync(new CreateReservationDto
        {
            HotelId = 1, RoomId = 1, GuestId = 2, BookingType = BookingType.ShortStay,
            CheckInDate = _day.AddHours(9), CheckOutDate = _day.AddHours(12),
            NumberOfGuests = 1
        });

        Assert.Equal(3, shortStay.DurationInHours);
    }

    [Fact]
    public async Task AvailableRooms_ExcludesRoomOnlyWhenStaysReallyOverlap()
    {
        await BookNightsAsync(1, _day, 2);

        var sameNights = await _service.GetAvailableRoomsAsync(1, _day, _day.AddDays(2), BookingType.Daily);
        var nextArrival = await _service.GetAvailableRoomsAsync(1, _day.AddDays(2), _day.AddDays(3), BookingType.Daily);

        Assert.Empty(sameNights);
        Assert.Single(nextArrival);
    }

    #endregion

    #region Booking validation

    [Fact]
    public async Task BlacklistedGuest_CannotBeBooked()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => BookNightsAsync(3, _day, 1));
        Assert.Contains("blacklisted", ex.Message);
    }

    [Fact]
    public async Task DepositAboveTotal_IsRejected()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => BookNightsAsync(1, _day, 1, deposit: 150));
    }

    [Fact]
    public async Task Update_ExceedingRoomCapacity_IsRejected()
    {
        var booking = await BookNightsAsync(1, _day, 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateReservationAsync(booking.Id, new UpdateReservationDto
        {
            CheckInDate = booking.CheckInDate, CheckOutDate = booking.CheckOutDate, NumberOfGuests = 5
        }));
    }

    #endregion

    #region Money

    [Fact]
    public async Task Deposit_IsRecordedInLedger()
    {
        var booking = await BookNightsAsync(1, _day, 2, deposit: 50);

        var payments = (await _service.GetPaymentsAsync(booking.Id)).ToList();

        Assert.Single(payments);
        Assert.Equal(50, payments[0].Amount);
        Assert.Equal(50, booking.DepositAmount);
        Assert.Equal(150, booking.RemainingAmount);
        Assert.Equal(PaymentStatus.PartiallyPaid, booking.PaymentStatus);
    }

    [Fact]
    public async Task PartialRefund_KeepsCancellationReason_AndReflectsNetPaid()
    {
        var booking = await BookNightsAsync(1, _day, 2, deposit: 200);
        await _service.CancelReservationAsync(booking.Id, "Change of plans");

        var afterPartial = await _service.RecordRefundAsync(booking.Id, 50, "Partial refund, fee retained");

        Assert.Equal("Change of plans", afterPartial.CancellationReason);
        Assert.Equal(150, afterPartial.DepositAmount);
        Assert.Equal(PaymentStatus.Refunding, afterPartial.PaymentStatus); // money still owed back

        var afterFull = await _service.RecordRefundAsync(booking.Id, 150);
        Assert.Equal(PaymentStatus.Refunded, afterFull.PaymentStatus);
        Assert.Equal(3, (await _service.GetPaymentsAsync(booking.Id)).Count());
    }

    [Fact]
    public async Task PriceOverride_IsStoredAsDiscount()
    {
        var booking = await BookNightsAsync(1, _day, 2); // 200

        var adjusted = await _service.ApplyPriceAdjustmentAsync(booking.Id, 0, null, overridePrice: 150);

        Assert.Equal(150, adjusted.TotalAmount);
        Assert.Equal(50, adjusted.DiscountAmount);
    }

    [Fact]
    public async Task Discount_BelowAlreadyPaid_IsRejected()
    {
        var booking = await BookNightsAsync(1, _day, 2, deposit: 180);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ApplyPriceAdjustmentAsync(booking.Id, 50, "Loyalty", null));
    }

    #endregion

    #region Lifecycle

    [Fact]
    public async Task CheckIn_IntoOccupiedRoom_IsRejected()
    {
        var booking = await BookNightsAsync(1, _day, 1);
        await _service.ConfirmReservationAsync(booking.Id);
        (await _context.Rooms.FindAsync(1))!.Status = RoomStatus.Occupied;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CheckInReservationAsync(booking.Id));
    }

    [Fact]
    public async Task CheckOut_RecordsGuestsLastStay()
    {
        var booking = await BookNightsAsync(1, _day, 1);
        await _service.ConfirmReservationAsync(booking.Id);
        await _service.CheckInReservationAsync(booking.Id);

        await _service.CheckOutReservationAsync(booking.Id);

        Assert.NotNull((await _context.Guests.FindAsync(1))!.LastStayDate);
    }

    [Fact]
    public async Task Delete_WithPayments_IsRejected()
    {
        var booking = await BookNightsAsync(1, _day, 1, deposit: 20);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.DeleteReservationAsync(booking.Id));
    }

    #endregion
}
