using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Implementations;
using HotelManagement.Services.Push;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Services;

/// <summary>Check-in / check-out reminders 30 minutes ahead, and who gets them where</summary>
public class StayReminderTests : IDisposable
{
    private const string Owner = "owner";
    private const string Endpoint = "https://push.example.com/browser-1";

    private readonly ApplicationDbContext _context;
    private readonly FixedTime _time = new(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
    private readonly Mock<IPushQueue> _push = new();
    private readonly Mock<IWebPushQueue> _webPush = new();
    private readonly NotificationService _service;

    public StayReminderTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _service = new NotificationService(_context, _push.Object, _webPush.Object,
            new Mock<IHttpContextAccessor>().Object, _time, NullLogger<NotificationService>.Instance);

        _context.Users.Add(new ApplicationUser { Id = Owner, UserName = Owner, FirstName = "Olga", LastName = "Owner" });
        _context.Hotels.Add(new Hotel { Id = 1, OwnerId = Owner, Name = "Test", Address = "A", City = "C", Country = "X", TimeZone = "UTC" });
        _context.Rooms.Add(new Room { Id = 1, HotelId = 1, RoomNumber = "101", Capacity = 2, PricePerNight = 100 });
        _context.Guests.Add(new Guest { Id = 1, FirstName = "Ana", LastName = "Guest", Email = "a@x.com", PhoneNumber = "1" });
        _context.WebPushSubscriptions.Add(new WebPushSubscription { UserId = Owner, Endpoint = Endpoint, P256dh = "key", Auth = "auth" });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private sealed class FixedTime(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }

    /// <summary>Stay times are hotel wall-clock, so they're stored without a kind</summary>
    private static DateTime Local(int hour, int minute = 0, int day = 5) =>
        new(2026, 11, day, hour, minute, 0, DateTimeKind.Unspecified);

    private Reservation AddReservation(DateTime checkIn, DateTime checkOut, ReservationStatus status,
        BookingType type = BookingType.ShortStay)
    {
        var reservation = new Reservation
        {
            HotelId = 1, RoomId = 1, GuestId = 1, CreatedByUserId = Owner,
            BookingType = type, CheckInDate = checkIn, CheckOutDate = checkOut, Status = status
        };
        _context.Reservations.Add(reservation);
        _context.SaveChanges();
        return reservation;
    }

    [Fact]
    public async Task CheckInWithin30Minutes_NotifiesManagersAndPushesToTheirBrowsers()
    {
        var reservation = AddReservation(Local(12, 20), Local(16, 20), ReservationStatus.Confirmed);

        var sent = await _service.SendStayRemindersAsync();

        Assert.Equal(1, sent);
        var notification = Assert.Single(_context.Notifications);
        Assert.Equal(NotificationType.CheckInSoon, notification.Type);
        Assert.Equal(Owner, notification.UserId);
        Assert.Equal(reservation.Id, notification.ReservationId);
        Assert.Equal("Check-in at 12:20", notification.Title);
        Assert.Equal("Ana Guest · Room 101", notification.Body);
        _webPush.Verify(q => q.Enqueue(It.Is<WebPushMessage>(m =>
            m.Endpoint == Endpoint && m.Url == $"/dashboard/reservations/{reservation.Id}")), Times.Once);
    }

    [Fact]
    public async Task Reminder_IsSentOnlyOnce()
    {
        AddReservation(Local(12, 20), Local(16, 20), ReservationStatus.Confirmed);

        await _service.SendStayRemindersAsync();
        _time.UtcNow = _time.UtcNow.AddMinutes(5);
        var secondRun = await _service.SendStayRemindersAsync();

        Assert.Equal(0, secondRun);
        Assert.Single(_context.Notifications);
    }

    [Theory]
    [InlineData(12, 45)] // more than 30 minutes away
    [InlineData(11, 55)] // already passed
    public async Task CheckInOutsideTheWindow_SendsNothing(int hour, int minute)
    {
        AddReservation(Local(hour, minute), Local(hour + 4, minute), ReservationStatus.Confirmed);

        Assert.Equal(0, await _service.SendStayRemindersAsync());
        Assert.Empty(_context.Notifications);
    }

    [Fact]
    public async Task CheckedInGuest_GetsACheckOutReminder()
    {
        AddReservation(Local(8), Local(12, 10), ReservationStatus.CheckedIn);

        await _service.SendStayRemindersAsync();

        var notification = Assert.Single(_context.Notifications);
        Assert.Equal(NotificationType.CheckOutSoon, notification.Type);
        Assert.Equal("Check-out at 12:10", notification.Title);
    }

    [Fact]
    public async Task DateOnlyOvernightStay_UsesTheHotelsCheckInTime()
    {
        var hotel = _context.Hotels.Single();
        hotel.CheckInTime = "12:15";
        _context.SaveChanges();
        AddReservation(Local(0), Local(0, day: 6), ReservationStatus.Confirmed, BookingType.Daily);

        await _service.SendStayRemindersAsync();

        Assert.Equal("Check-in at 12:15", Assert.Single(_context.Notifications).Title);
    }

    [Fact]
    public async Task HotelTimeZone_DecidesWhenTheReminderIsDue()
    {
        // 12:00 UTC is 13:00 in Belgrade in November
        var hotel = _context.Hotels.Single();
        hotel.TimeZone = "Europe/Belgrade";
        _context.SaveChanges();
        AddReservation(Local(12, 20), Local(16), ReservationStatus.Confirmed); // already passed there
        var due = AddReservation(Local(13, 20), Local(17), ReservationStatus.Confirmed);

        await _service.SendStayRemindersAsync();

        Assert.Equal(due.Id, Assert.Single(_context.Notifications).ReservationId);
    }

    [Fact]
    public async Task HotelWithoutTimeZone_SendsNothing()
    {
        var hotel = _context.Hotels.Single();
        hotel.TimeZone = null;
        _context.SaveChanges();
        AddReservation(Local(12, 20), Local(16, 20), ReservationStatus.Confirmed);

        Assert.Equal(0, await _service.SendStayRemindersAsync());
    }

    [Fact]
    public async Task MutedReminders_AreNotPushedButStayInTheList()
    {
        _context.NotificationPreferences.Add(new NotificationPreference { UserId = Owner, Type = NotificationType.CheckInSoon, PushEnabled = false });
        _context.SaveChanges();
        AddReservation(Local(12, 20), Local(16, 20), ReservationStatus.Confirmed);

        await _service.SendStayRemindersAsync();

        Assert.Single(_context.Notifications);
        _webPush.Verify(q => q.Enqueue(It.IsAny<WebPushMessage>()), Times.Never);
    }

    [Fact]
    public async Task RegisteringABrowser_FillsInTheHotelTimeZone()
    {
        var hotel = _context.Hotels.Single();
        hotel.TimeZone = null;
        _context.SaveChanges();

        await _service.RegisterWebPushAsync(Owner, new WebPushSubscriptionDto
        {
            Endpoint = "https://push.example.com/browser-2",
            Keys = new WebPushKeysDto { P256dh = "k", Auth = "a" },
            TimeZone = "Europe/Belgrade"
        });

        Assert.Equal("Europe/Belgrade", _context.Hotels.Single().TimeZone);
        Assert.Equal(2, _context.WebPushSubscriptions.Count());
    }
}
