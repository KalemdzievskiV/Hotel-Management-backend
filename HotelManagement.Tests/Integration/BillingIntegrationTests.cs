using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Billing;
using HotelManagement.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// An owner's billing through the API, paying with the fake provider's checkout
/// </summary>
public class BillingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestApi _api;

    public BillingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new TestApi(factory.CreateClient());
    }

    private async Task<(string Token, string Id)> SignUpOwnerAsync()
    {
        var email = $"owner{Guid.NewGuid():N}@test.com";
        var response = await _api.Client.PostAsJsonAsync("/api/Auth/register-owner", new RegisterOwnerRequestDto
        {
            FirstName = "Olga", LastName = "Owner", Email = email, Password = "Passw0rd"
        });
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        return (auth.Token, await _api.GetUserIdAsync(email));
    }

    private async Task<string> StartCheckoutAsync(string token, SubscriptionPlan plan, BillingInterval interval = BillingInterval.Monthly)
    {
        var checkout = await _api.PostAsync<CheckoutResponse>("/api/billing/checkout", token, new CheckoutRequest { Plan = plan, Interval = interval });
        checkout.CheckoutUrl.Should().StartWith("/dashboard/billing/checkout?session=");
        return checkout.CheckoutUrl.Split("session=")[1];
    }

    private Task<HttpResponseMessage> CompleteCheckoutAsync(string token, string session, bool approve = true) =>
        _api.SendAsync(HttpMethod.Post, $"/api/billing/fake/checkout/complete?session={session}", token,
            new CompleteFakeCheckoutRequest { Approve = approve });

    [Fact]
    public async Task Plans_ArePublic()
    {
        var plans = await _api.Client.GetFromJsonAsync<List<PlanDto>>("/api/billing/plans");

        plans!.Select(p => p.Name).Should().Equal("Free", "Starter", "Pro");
        plans![1].MonthlyPrice.Should().Be(12m);
        plans![2].MaxStaff.Should().BeNull();
    }

    [Fact]
    public async Task ANewOwner_SeesTheirTrialUsageAndHistory()
    {
        var (token, _) = await SignUpOwnerAsync();
        await _api.PostAsync<HotelDto>("/api/Hotels", token, new HotelDto { Name = "Lakeview", Address = "1", City = "Ohrid", Country = "MK" });

        var overview = await _api.GetAsync<BillingOverviewDto>("/api/billing", token);

        overview.CurrentPlan.Should().Be(SubscriptionPlan.Pro);
        overview.Status.Should().Be(SubscriptionStatus.Trialing);
        overview.DaysLeft.Should().Be(30);
        overview.CanCheckout.Should().BeTrue();
        overview.Usage.Hotels.Should().Be(1);
        overview.Limits.MaxHotels.Should().Be(3);
        overview.History.Should().ContainSingle(e => e.Type == SubscriptionEventType.TrialStarted);
    }

    [Fact]
    public async Task PayingAtTheFakeCheckout_StartsTheSubscription()
    {
        var (token, _) = await SignUpOwnerAsync();
        var session = await StartCheckoutAsync(token, SubscriptionPlan.Starter, BillingInterval.Yearly);

        var checkout = await _api.GetAsync<FakeCheckoutDto>($"/api/billing/fake/checkout?session={session}", token);
        checkout.PlanName.Should().Be("Starter");
        checkout.Amount.Should().Be(120m);
        checkout.Currency.Should().Be("EUR");

        var response = await CompleteCheckoutAsync(token, session);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var overview = (await response.Content.ReadFromJsonAsync<BillingOverviewDto>())!;
        overview.CurrentPlan.Should().Be(SubscriptionPlan.Starter);
        overview.Status.Should().Be(SubscriptionStatus.Active);
        overview.Interval.Should().Be(BillingInterval.Yearly);
        overview.CanCheckout.Should().BeFalse();
        overview.History.First().Amount.Should().Be(120m);
    }

    [Fact]
    public async Task ADeclinedTestCard_ChangesNothing()
    {
        var (token, _) = await SignUpOwnerAsync();
        var session = await StartCheckoutAsync(token, SubscriptionPlan.Starter);

        (await CompleteCheckoutAsync(token, session, approve: false)).StatusCode.Should().Be(HttpStatusCode.PaymentRequired);

        (await _api.GetAsync<BillingOverviewDto>("/api/billing", token)).Status.Should().Be(SubscriptionStatus.Trialing);
    }

    [Fact]
    public async Task ACheckoutLink_OnlyWorksForTheOwnerWhoStartedIt()
    {
        var (token, _) = await SignUpOwnerAsync();
        var (otherToken, _) = await SignUpOwnerAsync();
        var session = await StartCheckoutAsync(token, SubscriptionPlan.Pro);

        (await _api.SendAsync(HttpMethod.Get, $"/api/billing/fake/checkout?session={session}", otherToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CompleteCheckoutAsync(otherToken, session)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.SendAsync(HttpMethod.Get, "/api/billing/fake/checkout?session=forged.123", token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OwnersChangeCancelAndResumeTheirPlan()
    {
        var (token, _) = await SignUpOwnerAsync();
        await CompleteCheckoutAsync(token, await StartCheckoutAsync(token, SubscriptionPlan.Starter));

        (await _api.PostAsync<BillingOverviewDto>("/api/billing/change-plan", token, new ChangePlanRequest { Plan = SubscriptionPlan.Pro }))
            .CurrentPlan.Should().Be(SubscriptionPlan.Pro);
        (await _api.PostAsync<BillingOverviewDto>("/api/billing/cancel", token)).CancelAtPeriodEnd.Should().BeTrue();
        (await _api.PostAsync<BillingOverviewDto>("/api/billing/resume", token)).CancelAtPeriodEnd.Should().BeFalse();

        // Already paying: a second checkout is refused
        (await _api.SendAsync(HttpMethod.Post, "/api/billing/checkout", token, new CheckoutRequest { Plan = SubscriptionPlan.Pro }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TheWebhook_AcceptsOnlyCorrectlySignedNotifications()
    {
        var (token, ownerId) = await SignUpOwnerAsync();
        await CompleteCheckoutAsync(token, await StartCheckoutAsync(token, SubscriptionPlan.Starter));
        var provider = _factory.Services.GetRequiredService<FakeBillingProvider>();
        var subscriptionId = (await _api.GetAsync<BillingOverviewDto>("/api/billing", token)).History.First().Reference;

        var failed = new BillingEvent
        {
            Id = $"evt_{Guid.NewGuid():N}",
            Type = BillingEventType.PaymentFailed,
            OwnerId = ownerId,
            ProviderSubscriptionId = subscriptionId,
            Plan = SubscriptionPlan.Starter,
            OccurredAt = DateTime.UtcNow
        };
        var (body, signature) = provider.CreateWebhook(failed);

        async Task<HttpStatusCode> PostAsync(string? sig)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhooks/fake")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            if (sig != null)
                request.Headers.Add("X-Signature", sig);
            return (await _api.Client.SendAsync(request)).StatusCode;
        }

        (await PostAsync(null)).Should().Be(HttpStatusCode.Unauthorized);
        (await PostAsync("DEADBEEF")).Should().Be(HttpStatusCode.Unauthorized);
        (await PostAsync(signature)).Should().Be(HttpStatusCode.OK);

        var overview = await _api.GetAsync<BillingOverviewDto>("/api/billing", token);
        overview.Status.Should().Be(SubscriptionStatus.PastDue);
        overview.CurrentPlan.Should().Be(SubscriptionPlan.Starter);
    }

    [Fact]
    public async Task SuperAdmins_CanSimulateRenewalsAndRunTheJob()
    {
        var (token, ownerId) = await SignUpOwnerAsync();
        await CompleteCheckoutAsync(token, await StartCheckoutAsync(token, SubscriptionPlan.Starter));
        var superAdminToken = await TestAuth.GetTokenAsync(_api.Client, "SuperAdmin");

        (await _api.PostAsync<BillingOverviewDto>("/api/billing/fake/simulate", superAdminToken,
            new SimulateBillingRequest { OwnerId = ownerId, Action = SimulateBillingAction.RenewalFailed }))
            .Status.Should().Be(SubscriptionStatus.PastDue);

        var renewed = await _api.PostAsync<BillingOverviewDto>("/api/billing/fake/simulate", superAdminToken,
            new SimulateBillingRequest { OwnerId = ownerId, Action = SimulateBillingAction.RenewalPaid });
        renewed.Status.Should().Be(SubscriptionStatus.Active);
        renewed.AccessUntil.Should().BeCloseTo(DateTime.UtcNow.AddMonths(2), TimeSpan.FromMinutes(1));

        (await _api.SendAsync(HttpMethod.Post, "/api/billing/fake/simulate", superAdminToken,
            new SimulateBillingRequest { Action = SimulateBillingAction.RunMaintenance })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Owners can't
        (await _api.SendAsync(HttpMethod.Post, "/api/billing/fake/simulate", token,
            new SimulateBillingRequest { OwnerId = ownerId, Action = SimulateBillingAction.RenewalPaid })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Housekeeper")]
    [InlineData("Guest")]
    public async Task Billing_IsForHotelOwners(string role)
    {
        var token = await TestAuth.GetTokenAsync(_api.Client, role);

        (await _api.SendAsync(HttpMethod.Get, "/api/billing", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, "/api/billing/checkout", token, new CheckoutRequest { Plan = SubscriptionPlan.Pro }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
