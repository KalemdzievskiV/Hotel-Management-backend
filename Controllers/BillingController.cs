using System.Security.Claims;
using HotelManagement.Data;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Services.Billing;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Controllers;

/// <summary>
/// A hotel owner's own subscription, the public plan list, and payment-provider webhooks
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billing;

    public BillingController(IBillingService billing)
    {
        _billing = billing;
    }

    private string OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>
    /// Plans and prices, for the public pricing page
    /// </summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public IActionResult GetPlans() => Ok(PlanCatalog.All.Select(PlanDto.From));

    [HttpGet]
    public async Task<IActionResult> GetOverview() => Ok(await _billing.GetOverviewAsync(OwnerId));

    [HttpPost("checkout")]
    public async Task<IActionResult> StartCheckout([FromBody] CheckoutRequest request)
    {
        var session = await _billing.StartCheckoutAsync(OwnerId, request.Plan, request.Interval);
        return Ok(new CheckoutResponse { CheckoutUrl = session.CheckoutUrl });
    }

    [HttpPost("change-plan")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request) =>
        Ok(await _billing.ChangePlanAsync(OwnerId, request.Plan));

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel() => Ok(await _billing.CancelAsync(OwnerId));

    [HttpPost("resume")]
    public async Task<IActionResult> Resume() => Ok(await _billing.ResumeAsync(OwnerId));

    /// <summary>
    /// Notifications from a payment provider. Authenticated by the provider's signature, not a user.
    /// </summary>
    [HttpPost("webhooks/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(string provider)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        await _billing.HandleWebhookAsync(provider, body, Request.Headers["X-Signature"].FirstOrDefault());
        return Ok();
    }
}

/// <summary>
/// The pretend checkout used until a real payment provider is chosen. Its "pay" button produces
/// the same signed notification a real provider would send, handled by the normal webhook code.
/// </summary>
[ApiController]
[Route("api/billing/fake")]
[Authorize]
public class FakeBillingController : ControllerBase
{
    private readonly FakeBillingProvider _provider;
    private readonly IBillingService _billing;
    private readonly IWebHostEnvironment _environment;

    public FakeBillingController(FakeBillingProvider provider, IBillingService billing, IWebHostEnvironment environment)
    {
        _provider = provider;
        _billing = billing;
        _environment = environment;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private FakeCheckout ReadOwnCheckout(string session)
    {
        var checkout = _provider.ReadCheckout(session);
        if (checkout.OwnerId != CurrentUserId)
            throw new KeyNotFoundException("Checkout not found");
        return checkout;
    }

    [HttpGet("checkout")]
    [Authorize(Roles = AppRoles.Admin)]
    public IActionResult GetCheckout([FromQuery] string session)
    {
        var checkout = ReadOwnCheckout(session);
        return Ok(new FakeCheckoutDto
        {
            Plan = checkout.Plan,
            PlanName = PlanCatalog.Get(checkout.Plan).Name,
            Interval = checkout.Interval,
            Amount = checkout.Amount,
            Currency = checkout.Currency,
            ExpiresAt = checkout.ExpiresAt
        });
    }

    [HttpPost("checkout/complete")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CompleteCheckout([FromQuery] string session, [FromBody] CompleteFakeCheckoutRequest request)
    {
        var checkout = ReadOwnCheckout(session);
        if (!request.Approve)
            return StatusCode(StatusCodes.Status402PaymentRequired, new { message = "The card was declined (test). Nothing was charged." });

        var (body, signature) = _provider.CompleteCheckout(checkout);
        await _billing.HandleWebhookAsync(FakeBillingProvider.ProviderName, body, signature);
        return Ok(await _billing.GetOverviewAsync(CurrentUserId));
    }

    /// <summary>
    /// For trying billing out: renew or fail the next payment of an owner's fake subscription
    /// now, or run the renew-and-expire job. Not available in production.
    /// </summary>
    [HttpPost("simulate")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Simulate([FromBody] SimulateBillingRequest request, [FromServices] ApplicationDbContext context)
    {
        if (_environment.IsProduction())
            return NotFound();

        if (request.Action == SimulateBillingAction.RunMaintenance)
            return Ok(new { changed = await _billing.RunMaintenanceAsync() });

        if (string.IsNullOrEmpty(request.OwnerId))
            return BadRequest(new { message = "OwnerId is required" });

        var subscription = await context.Subscriptions.FirstOrDefaultAsync(s => s.OwnerId == request.OwnerId);
        if (subscription == null || subscription.Source != _provider.Source || subscription.ProviderSubscriptionId == null)
            return BadRequest(new { message = "This owner doesn't pay through the fake provider" });

        var (body, signature) = _provider.CreateWebhook(
            _provider.CreateRenewal(subscription, paid: request.Action == SimulateBillingAction.RenewalPaid));
        await _billing.HandleWebhookAsync(FakeBillingProvider.ProviderName, body, signature);
        return Ok(await _billing.GetOverviewAsync(request.OwnerId));
    }
}
