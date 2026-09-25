using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

public class AdminSubscriptionsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly TestApi _api;

    public AdminSubscriptionsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
    }

    private async Task<(string Token, string Id, string Email)> SignUpOwnerAsync()
    {
        var email = $"owner{Guid.NewGuid():N}@test.com";
        var response = await _api.Client.PostAsJsonAsync("/api/Auth/register-owner", new RegisterOwnerRequestDto
        {
            FirstName = "Olga", LastName = "Owner", Email = email, Password = "Passw0rd"
        });
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        return (auth.Token, await _api.GetUserIdAsync(email), email);
    }

    [Fact]
    public async Task TheSuperAdmin_ExtendsATrial_AndTheOwnerSeesItWithTheReason()
    {
        var (ownerToken, ownerId, email) = await SignUpOwnerAsync();
        var superAdminToken = await TestAuth.GetTokenAsync(_api.Client, "SuperAdmin");

        var listed = await _api.GetAsync<List<SubscriptionSummaryDto>>($"/api/admin/subscriptions?filter=Trialing&search={email}", superAdminToken);
        listed.Should().ContainSingle(s => s.OwnerId == ownerId && s.DaysLeft == 30);

        var detail = await _api.PostAsync<SubscriptionDetailDto>($"/api/admin/subscriptions/{ownerId}/extend", superAdminToken,
            new ExtendSubscriptionRequest { Days = 15, Reason = "Needs time to set up all rooms" });
        detail.Summary.DaysLeft.Should().Be(45);

        var seenByOwner = await _api.GetAsync<BillingOverviewDto>("/api/billing", ownerToken);
        seenByOwner.DaysLeft.Should().Be(45);
        var entry = seenByOwner.History.First();
        entry.Type.Should().Be(SubscriptionEventType.TrialExtended);
        entry.Reason.Should().Be("Needs time to set up all rooms");
        entry.ActorName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EveryChange_NeedsAReason()
    {
        var (_, ownerId, _) = await SignUpOwnerAsync();
        var superAdminToken = await TestAuth.GetTokenAsync(_api.Client, "SuperAdmin");

        (await _api.SendAsync(HttpMethod.Post, $"/api/admin/subscriptions/{ownerId}/extend", superAdminToken,
            new ExtendSubscriptionRequest { Days = 15, Reason = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _api.SendAsync(HttpMethod.Post, $"/api/admin/subscriptions/{ownerId}/grant-access", superAdminToken,
            new { Plan = "Pro", Until = DateTime.UtcNow.AddMonths(1) })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ABankTransfer_IsRecordedAndShowsInTheListAndStats()
    {
        var (ownerToken, ownerId, _) = await SignUpOwnerAsync();
        var superAdminToken = await TestAuth.GetTokenAsync(_api.Client, "SuperAdmin");

        await _api.PostAsync<SubscriptionDetailDto>($"/api/admin/subscriptions/{ownerId}/manual-payment", superAdminToken,
            new ManualPaymentRequest { Plan = SubscriptionPlan.Starter, Until = DateTime.UtcNow.AddYears(1), Amount = 120m, Reference = "INV-7", Reason = "Paid by bank transfer" });

        (await _api.GetAsync<List<SubscriptionSummaryDto>>("/api/admin/subscriptions?filter=Manual", superAdminToken))
            .Should().Contain(s => s.OwnerId == ownerId && s.Source == BillingSource.Manual && s.MonthlyRevenue == 12m);
        (await _api.GetAsync<SubscriptionStatsDto>("/api/admin/subscriptions/stats", superAdminToken)).Paying.Should().BeGreaterThan(0);

        var billing = await _api.GetAsync<BillingOverviewDto>("/api/billing", ownerToken);
        billing.CurrentPlan.Should().Be(SubscriptionPlan.Starter);
        billing.History.First().Amount.Should().Be(120m);
    }

    [Fact]
    public async Task OnlySuperAdmins_ManageSubscriptions()
    {
        var (ownerToken, ownerId, _) = await SignUpOwnerAsync();

        (await _api.SendAsync(HttpMethod.Get, "/api/admin/subscriptions", ownerToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, $"/api/admin/subscriptions/{ownerId}/extend", ownerToken,
            new ExtendSubscriptionRequest { Days = 300, Reason = "Giving myself a year" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var managerToken = await TestAuth.GetTokenAsync(_api.Client, "Manager");
        (await _api.SendAsync(HttpMethod.Get, "/api/admin/subscriptions/stats", managerToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AUserWhoIsNotAnOwner_HasNoSubscriptionToManage()
    {
        var superAdminToken = await TestAuth.GetTokenAsync(_api.Client, "SuperAdmin");
        var guestEmail = $"guest{Guid.NewGuid():N}@test.com";
        await TestAuth.GetTokenAsync(_api.Client, "Guest", guestEmail);
        var guestId = await _api.GetUserIdAsync(guestEmail);

        (await _api.SendAsync(HttpMethod.Get, $"/api/admin/subscriptions/{guestId}", superAdminToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
