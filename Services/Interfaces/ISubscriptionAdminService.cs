using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces;

/// <summary>
/// SuperAdmin tools for hotel owners' subscriptions. Every change needs a reason and is
/// recorded in the subscription's history with who made it.
/// </summary>
public interface ISubscriptionAdminService
{
    Task<List<SubscriptionSummaryDto>> ListAsync(SubscriptionFilter filter, string? search);

    Task<SubscriptionStatsDto> GetStatsAsync();

    Task<SubscriptionDetailDto> GetAsync(string ownerId);

    /// <summary>Lengthens the trial, the paid period or free access</summary>
    Task<SubscriptionDetailDto> ExtendAsync(string ownerId, ExtendSubscriptionRequest request, string actorId);

    Task<SubscriptionDetailDto> RecordManualPaymentAsync(string ownerId, ManualPaymentRequest request, string actorId);

    Task<SubscriptionDetailDto> GrantAccessAsync(string ownerId, GrantAccessRequest request, string actorId);

    Task<SubscriptionDetailDto> ChangePlanAsync(string ownerId, AdminChangePlanRequest request, string actorId);

    Task<SubscriptionDetailDto> GrantGraceAsync(string ownerId, GrantGraceRequest request, string actorId);
}
