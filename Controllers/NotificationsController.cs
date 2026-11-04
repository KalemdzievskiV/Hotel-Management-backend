using System.Security.Claims;
using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using HotelManagement.Services.Push;

namespace HotelManagement.Controllers;

/// <summary>
/// The signed-in user's notifications, the phones that get their pushes, and which types push
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly PushOptions _pushOptions;
    private readonly IWebPushSender _webPushSender;

    public NotificationsController(INotificationService notifications, IOptions<PushOptions> pushOptions, IWebPushSender webPushSender)
    {
        _notifications = notifications;
        _pushOptions = pushOptions.Value;
        _webPushSender = webPushSender;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private List<string> Roles => User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    /// <summary>Newest first</summary>
    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _notifications.GetPageAsync(UserId, page, pageSize));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount() =>
        Ok(new { count = await _notifications.GetUnreadCountAsync(UserId) });

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id) =>
        await _notifications.MarkReadAsync(UserId, id) ? NoContent() : NotFound();

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(UserId);
        return NoContent();
    }

    /// <summary>The app calls this on every start while signed in, so the token stays current</summary>
    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceDto dto)
    {
        await _notifications.RegisterDeviceAsync(UserId, dto);
        return NoContent();
    }

    /// <summary>Called on sign-out, before the session ends</summary>
    [HttpDelete("devices")]
    public async Task<IActionResult> UnregisterDevice([FromBody] UnregisterDeviceDto dto)
    {
        await _notifications.UnregisterDeviceAsync(UserId, dto.Token);
        return NoContent();
    }

    /// <summary>The VAPID public key browsers subscribe with; 404 while web push isn't set up</summary>
    [HttpGet("web-push/key")]
    public IActionResult GetWebPushKey() =>
        _pushOptions.WebPushEnabled ? Ok(new { publicKey = _pushOptions.VapidPublicKey }) : NotFound();

    [HttpPost("web-push")]
    public async Task<IActionResult> RegisterWebPush([FromBody] WebPushSubscriptionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Endpoint) || !Uri.TryCreate(dto.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(dto.Keys.P256dh) || string.IsNullOrWhiteSpace(dto.Keys.Auth))
            return BadRequest(new { message = "Invalid push subscription" });

        await _notifications.RegisterWebPushAsync(UserId, dto);
        return NoContent();
    }

    [HttpDelete("web-push")]
    public async Task<IActionResult> UnregisterWebPush([FromBody] UnregisterWebPushDto dto)
    {
        await _notifications.UnregisterWebPushAsync(UserId, dto.Endpoint);
        return NoContent();
    }

    /// <summary>
    /// Sends a test push to this browser right away and reports what the push service said,
    /// so a broken setup shows its error instead of failing silently
    /// </summary>
    [HttpPost("web-push/test")]
    public async Task<IActionResult> TestWebPush([FromBody] TestWebPushDto dto, CancellationToken cancellationToken)
    {
        if (!_pushOptions.WebPushEnabled)
            return NotFound(new { message = "Push notifications aren't set up on the server" });

        var message = await _notifications.BuildTestWebPushAsync(UserId, dto.Endpoint);
        if (message == null)
            return NotFound(new { message = "This device isn't registered for notifications. Turn them off and on again." });

        var result = await _webPushSender.SendAsync(message, cancellationToken);
        switch (result.Status)
        {
            case WebPushSendStatus.Sent:
                return NoContent();
            case WebPushSendStatus.Gone:
                await _notifications.UnregisterWebPushAsync(UserId, dto.Endpoint);
                return StatusCode(StatusCodes.Status410Gone,
                    new { message = "This device's notification subscription expired. Turn notifications off and on again." });
            default:
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { message = $"The push service refused the notification: {result.Error}" });
        }
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences() =>
        Ok(await _notifications.GetPreferencesAsync(UserId, Roles));

    /// <summary>Changes only the types sent; types that don't apply to the user are ignored</summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] List<NotificationPreferenceDto> changes) =>
        Ok(await _notifications.UpdatePreferencesAsync(UserId, Roles, changes));
}
