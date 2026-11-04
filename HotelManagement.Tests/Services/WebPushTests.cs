using System.Security.Claims;
using HotelManagement.Controllers;
using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Implementations;
using HotelManagement.Services.Push;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Services;

/// <summary>Which events reach browsers, and the "Send test" endpoint</summary>
public class WebPushTests : IDisposable
{
    private const string Owner = "owner";
    private const string Other = "other";
    private const string Endpoint = "https://push.example.com/browser-1";

    private readonly ApplicationDbContext _context;
    private readonly Mock<IWebPushQueue> _webPush = new();
    private readonly Mock<IWebPushSender> _sender = new();
    private readonly NotificationService _service;

    public WebPushTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _service = new NotificationService(_context, new Mock<IPushQueue>().Object, _webPush.Object,
            new Mock<IHttpContextAccessor>().Object, TimeProvider.System, NullLogger<NotificationService>.Instance);

        _context.Users.Add(new ApplicationUser { Id = Owner, UserName = Owner, FirstName = "Olga", LastName = "Owner", IsActive = true });
        _context.Hotels.Add(new Hotel { Id = 1, OwnerId = Owner, Name = "Test", Address = "A", City = "C", Country = "X", TimeZone = "UTC" });
        _context.WebPushSubscriptions.Add(new WebPushSubscription { UserId = Owner, Endpoint = Endpoint, P256dh = "key", Auth = "auth" });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private NotificationsController Controller(string userId, bool webPushEnabled = true)
    {
        var options = Options.Create(new PushOptions
        {
            VapidPublicKey = webPushEnabled ? "public" : null,
            VapidPrivateKey = webPushEnabled ? "private" : null
        });
        return new NotificationsController(_service, options, _sender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"))
                }
            }
        };
    }

    [Fact]
    public async Task NewOnlineBooking_PushesToTheOwnersBrowser()
    {
        await _service.BookingRequestedAsync(new ReservationDto
        {
            Id = 7, HotelId = 1, GuestName = "Ana Guest", RoomNumber = "101",
            CheckInDate = new DateTime(2026, 11, 10), CheckOutDate = new DateTime(2026, 11, 12)
        });

        _webPush.Verify(q => q.Enqueue(It.Is<WebPushMessage>(m =>
            m.Endpoint == Endpoint && m.Title == "New booking to approve" && m.Url == "/dashboard/reservations/7")), Times.Once);
    }

    [Fact]
    public async Task TestPush_SendsToTheUsersOwnBrowser()
    {
        _sender.Setup(s => s.SendAsync(It.IsAny<WebPushMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebPushSendResult(WebPushSendStatus.Sent));

        var result = await Controller(Owner).TestWebPush(new TestWebPushDto { Endpoint = Endpoint }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _sender.Verify(s => s.SendAsync(It.Is<WebPushMessage>(m => m.Endpoint == Endpoint && m.Title == "Test notification"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestPush_ToAnotherUsersBrowser_IsNotFoundAndSendsNothing()
    {
        var result = await Controller(Other).TestWebPush(new TestWebPushDto { Endpoint = Endpoint }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        _sender.Verify(s => s.SendAsync(It.IsAny<WebPushMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestPush_WhenServerHasNoKeys_IsNotFound()
    {
        var result = await Controller(Owner, webPushEnabled: false)
            .TestWebPush(new TestWebPushDto { Endpoint = Endpoint }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task TestPush_RefusedByPushService_Returns502WithTheReason()
    {
        _sender.Setup(s => s.SendAsync(It.IsAny<WebPushMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebPushSendResult(WebPushSendStatus.Failed, "403 invalid VAPID"));

        var result = await Controller(Owner).TestWebPush(new TestWebPushDto { Endpoint = Endpoint }, CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.Contains("403 invalid VAPID", error.Value!.ToString());
    }

    [Fact]
    public async Task TestPush_ExpiredSubscription_IsForgotten()
    {
        _sender.Setup(s => s.SendAsync(It.IsAny<WebPushMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebPushSendResult(WebPushSendStatus.Gone, "410 Gone"));

        var result = await Controller(Owner).TestWebPush(new TestWebPushDto { Endpoint = Endpoint }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status410Gone, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Empty(_context.WebPushSubscriptions);
    }
}
