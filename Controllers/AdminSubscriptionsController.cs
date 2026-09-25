using System.Security.Claims;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Controllers;

/// <summary>
/// SuperAdmin view and tools for hotel owners' subscriptions
/// </summary>
[ApiController]
[Route("api/admin/subscriptions")]
[Authorize(Roles = AppRoles.SuperAdmin)]
public class AdminSubscriptionsController : ControllerBase
{
    private readonly ISubscriptionAdminService _subscriptions;

    public AdminSubscriptionsController(ISubscriptionAdminService subscriptions)
    {
        _subscriptions = subscriptions;
    }

    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SubscriptionFilter filter = SubscriptionFilter.All, [FromQuery] string? search = null) =>
        Ok(await _subscriptions.ListAsync(filter, search));

    [HttpGet("stats")]
    public async Task<IActionResult> Stats() => Ok(await _subscriptions.GetStatsAsync());

    [HttpGet("{ownerId}")]
    public async Task<IActionResult> Get(string ownerId) => Ok(await _subscriptions.GetAsync(ownerId));

    [HttpPost("{ownerId}/extend")]
    public async Task<IActionResult> Extend(string ownerId, [FromBody] ExtendSubscriptionRequest request) =>
        Ok(await _subscriptions.ExtendAsync(ownerId, request, ActorId));

    [HttpPost("{ownerId}/manual-payment")]
    public async Task<IActionResult> RecordManualPayment(string ownerId, [FromBody] ManualPaymentRequest request) =>
        Ok(await _subscriptions.RecordManualPaymentAsync(ownerId, request, ActorId));

    [HttpPost("{ownerId}/grant-access")]
    public async Task<IActionResult> GrantAccess(string ownerId, [FromBody] GrantAccessRequest request) =>
        Ok(await _subscriptions.GrantAccessAsync(ownerId, request, ActorId));

    [HttpPost("{ownerId}/change-plan")]
    public async Task<IActionResult> ChangePlan(string ownerId, [FromBody] AdminChangePlanRequest request) =>
        Ok(await _subscriptions.ChangePlanAsync(ownerId, request, ActorId));

    [HttpPost("{ownerId}/grace")]
    public async Task<IActionResult> GrantGrace(string ownerId, [FromBody] GrantGraceRequest request) =>
        Ok(await _subscriptions.GrantGraceAsync(ownerId, request, ActorId));
}
