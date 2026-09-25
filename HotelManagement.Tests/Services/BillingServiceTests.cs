using System.Security.Claims;
using FluentAssertions;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Models.Constants;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Billing;
using HotelManagement.Services.Implementations;
using HotelManagement.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Services;

/// <summary>
/// A subscription's life with the fake provider, with the clock moved by the tests
/// </summary>
public class BillingServiceTests
{
    private const string Owner = "owner";

    private readonly ApplicationDbContext _context;
    private readonly TestClock _clock = new();
    private readonly FakeBillingProvider _provider;
    private readonly BillingService _billing;

    public BillingServiceTests()
    {
        _context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var httpContext = new Mock<IHttpContextAccessor>();
        httpContext.Setup(x => x.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, AppRoles.Admin) }, "Test"))
        });

        _provider = new FakeBillingProvider(Options.Create(new BillingOptions { FakeSigningKey = "test-key" }), _clock);
        var entitlements = new EntitlementService(_context, httpContext.Object, _clock);
        _billing = new BillingService(_context, entitlements, new[] { _provider }, _clock, NullLogger<BillingService>.Instance);

        _context.Roles.Add(new IdentityRole(AppRoles.Admin) { Id = AppRoles.Admin });
        _context.Users.Add(new ApplicationUser { Id = Owner, UserName = Owner, FirstName = "Olga", LastName = "Owner" });
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = Owner, RoleId = AppRoles.Admin });
        _context.SaveChanges();
    }

    private DateTime Now => _clock.UtcDateTime;

    private Task<Subscription> GetSubscriptionAsync() =>
        _context.Subscriptions.Include(s => s.Events).SingleAsync(s => s.OwnerId == Owner);

    /// <summary>
    /// Goes through checkout and approves the test payment, as the fake checkout page does
    /// </summary>
    private async Task SubscribeAsync(SubscriptionPlan plan, BillingInterval interval = BillingInterval.Monthly)
    {
        var session = await _billing.StartCheckoutAsync(Owner, plan, interval);
        var token = session.CheckoutUrl.Split("session=")[1];
        var (body, signature) = _provider.CompleteCheckout(_provider.ReadCheckout(token));
        await _billing.HandleWebhookAsync(FakeBillingProvider.ProviderName, body, signature);
    }

    [Fact]
    public async Task PayingAtCheckout_StartsThePaidPlanForOnePeriod()
    {
        await SubscribeAsync(SubscriptionPlan.Starter);

        var subscription = await GetSubscriptionAsync();
        subscription.Plan.Should().Be(SubscriptionPlan.Starter);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.Source.Should().Be(BillingSource.Fake);
        subscription.AccessUntil.Should().Be(Now.AddMonths(1));
        subscription.ProviderSubscriptionId.Should().StartWith("fake_sub_");
        subscription.Events.Should().ContainSingle(e => e.Type == SubscriptionEventType.Subscribed && e.Amount == 12m);

        var overview = await _billing.GetOverviewAsync(Owner);
        overview.CurrentPlan.Should().Be(SubscriptionPlan.Starter);
        overview.CanCheckout.Should().BeFalse();
    }

    [Fact]
    public async Task YearlyBilling_ChargesTheYearlyPriceForAYear()
    {
        await SubscribeAsync(SubscriptionPlan.Pro, BillingInterval.Yearly);

        var subscription = await GetSubscriptionAsync();
        subscription.AccessUntil.Should().Be(Now.AddYears(1));
        subscription.Events.Single(e => e.Type == SubscriptionEventType.Subscribed).Amount.Should().Be(290m);
    }

    [Fact]
    public async Task Checkout_IsRefusedForFreeAndWhileAlreadyPaying()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _billing.StartCheckoutAsync(Owner, SubscriptionPlan.Free, BillingInterval.Monthly));

        await SubscribeAsync(SubscriptionPlan.Starter);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _billing.StartCheckoutAsync(Owner, SubscriptionPlan.Pro, BillingInterval.Monthly));
    }

    [Fact]
    public async Task CheckoutLinks_CannotBeAlteredOrReusedAfterTheyExpire()
    {
        var session = await _billing.StartCheckoutAsync(Owner, SubscriptionPlan.Starter, BillingInterval.Monthly);
        var token = session.CheckoutUrl.Split("session=")[1];

        Assert.Throws<BusinessRuleException>(() => _provider.ReadCheckout(token.Replace('A', 'B') + "x"));
        Assert.Throws<BusinessRuleException>(() => _provider.ReadCheckout("not-a-token"));

        _clock.Advance(TimeSpan.FromHours(2));
        Assert.Throws<BusinessRuleException>(() => _provider.ReadCheckout(token));
    }

    [Fact]
    public async Task Webhooks_WithAWrongSignatureAreRejected_AndEachEventAppliesOnce()
    {
        var session = await _billing.StartCheckoutAsync(Owner, SubscriptionPlan.Starter, BillingInterval.Monthly);
        var (body, signature) = _provider.CompleteCheckout(_provider.ReadCheckout(session.CheckoutUrl.Split("session=")[1]));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _billing.HandleWebhookAsync("fake", body, "0000"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _billing.HandleWebhookAsync("fake", body.Replace("Starter", "Pro"), signature));

        await _billing.HandleWebhookAsync("fake", body, signature);
        await _billing.HandleWebhookAsync("fake", body, signature);

        (await GetSubscriptionAsync()).Events.Count(e => e.Type == SubscriptionEventType.Subscribed).Should().Be(1);
        (await _context.ProcessedBillingEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AnUnpaidTrial_DropsToFreeWhenItEnds()
    {
        await _billing.GetOverviewAsync(Owner);
        _clock.Advance(TimeSpan.FromDays(PlanCatalog.TrialDays - 1));
        (await _billing.RunMaintenanceAsync()).Should().Be(0);

        _clock.Advance(TimeSpan.FromDays(2));
        (await _billing.RunMaintenanceAsync()).Should().Be(1);

        var subscription = await GetSubscriptionAsync();
        subscription.Plan.Should().Be(SubscriptionPlan.Free);
        subscription.Status.Should().Be(SubscriptionStatus.Expired);
        subscription.Events.OrderBy(e => e.Id).Last().Reason.Should().Be("Trial ended");

        // After the trial the owner can still subscribe
        (await _billing.GetOverviewAsync(Owner)).CanCheckout.Should().BeTrue();
    }

    [Fact]
    public async Task ThePaidPlan_RenewsAtTheEndOfEachPeriod()
    {
        await SubscribeAsync(SubscriptionPlan.Starter);
        var firstEnd = (await GetSubscriptionAsync()).AccessUntil!.Value;

        _clock.Advance(TimeSpan.FromDays(31));
        await _billing.RunMaintenanceAsync();

        var subscription = await GetSubscriptionAsync();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.AccessUntil.Should().Be(firstEnd.AddMonths(1));
        subscription.Events.Should().ContainSingle(e => e.Type == SubscriptionEventType.Renewed && e.Amount == 12m);
    }

    [Fact]
    public async Task RenewalsMissedWhileTheAppWasDown_AreCaughtUp()
    {
        await SubscribeAsync(SubscriptionPlan.Starter);

        _clock.Advance(TimeSpan.FromDays(100));
        await _billing.RunMaintenanceAsync();

        var subscription = await GetSubscriptionAsync();
        subscription.Plan.Should().Be(SubscriptionPlan.Starter);
        subscription.AccessUntil.Should().BeAfter(Now);
        subscription.Events.Count(e => e.Type == SubscriptionEventType.Renewed).Should().Be(3);
    }

    [Fact]
    public async Task AFailedPayment_KeepsThePlanDuringGrace_ThenDropsToFree()
    {
        await SubscribeAsync(SubscriptionPlan.Pro);
        var subscription = await GetSubscriptionAsync();
        _clock.Advance(TimeSpan.FromDays(31));

        var (body, signature) = _provider.CreateWebhook(_provider.CreateRenewal(subscription, paid: false));
        await _billing.HandleWebhookAsync("fake", body, signature);

        subscription = await GetSubscriptionAsync();
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
        subscription.GraceUntil.Should().Be(Now.AddDays(PlanCatalog.GraceDays));
        (await _billing.GetOverviewAsync(Owner)).CurrentPlan.Should().Be(SubscriptionPlan.Pro);
        (await _billing.GetOverviewAsync(Owner)).CanCheckout.Should().BeTrue(); // to pay with another card

        // The background job doesn't expire it during grace, nor charge it again
        await _billing.RunMaintenanceAsync();
        (await GetSubscriptionAsync()).Status.Should().Be(SubscriptionStatus.PastDue);

        _clock.Advance(TimeSpan.FromDays(PlanCatalog.GraceDays + 1));
        await _billing.RunMaintenanceAsync();

        subscription = await GetSubscriptionAsync();
        subscription.Plan.Should().Be(SubscriptionPlan.Free);
        subscription.Events.OrderBy(e => e.Id).Last().Reason.Should().Be("Payment wasn't received");
    }

    [Fact]
    public async Task PayingAfterAFailedPayment_EndsTheGracePeriod()
    {
        await SubscribeAsync(SubscriptionPlan.Starter);
        var subscription = await GetSubscriptionAsync();
        var (body, signature) = _provider.CreateWebhook(_provider.CreateRenewal(subscription, paid: false));
        await _billing.HandleWebhookAsync("fake", body, signature);

        (body, signature) = _provider.CreateWebhook(_provider.CreateRenewal(await GetSubscriptionAsync(), paid: true));
        await _billing.HandleWebhookAsync("fake", body, signature);

        subscription = await GetSubscriptionAsync();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.GraceUntil.Should().BeNull();
    }

    [Fact]
    public async Task Cancelling_KeepsThePlanToTheEndOfThePaidPeriod_AndCanBeUndone()
    {
        await SubscribeAsync(SubscriptionPlan.Starter);

        var overview = await _billing.CancelAsync(Owner);
        overview.CancelAtPeriodEnd.Should().BeTrue();
        overview.CurrentPlan.Should().Be(SubscriptionPlan.Starter);

        (await _billing.ResumeAsync(Owner)).CancelAtPeriodEnd.Should().BeFalse();
        await _billing.CancelAsync(Owner);

        _clock.Advance(TimeSpan.FromDays(31));
        await _billing.RunMaintenanceAsync();

        var subscription = await GetSubscriptionAsync();
        subscription.Plan.Should().Be(SubscriptionPlan.Free);
        subscription.Events.Should().NotContain(e => e.Type == SubscriptionEventType.Renewed);
        subscription.Events.OrderBy(e => e.Id).Last().Reason.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Upgrades_ApplyNow_DowngradesAtTheNextRenewal()
    {
        await SubscribeAsync(SubscriptionPlan.Starter);

        (await _billing.ChangePlanAsync(Owner, SubscriptionPlan.Pro)).CurrentPlan.Should().Be(SubscriptionPlan.Pro);

        var overview = await _billing.ChangePlanAsync(Owner, SubscriptionPlan.Starter);
        overview.CurrentPlan.Should().Be(SubscriptionPlan.Pro);
        overview.ScheduledPlan.Should().Be(SubscriptionPlan.Starter);

        _clock.Advance(TimeSpan.FromDays(31));
        await _billing.RunMaintenanceAsync();

        var subscription = await GetSubscriptionAsync();
        subscription.Plan.Should().Be(SubscriptionPlan.Starter);
        subscription.ScheduledPlan.Should().BeNull();
        subscription.Events.Single(e => e.Type == SubscriptionEventType.Renewed).Amount.Should().Be(12m);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _billing.ChangePlanAsync(Owner, SubscriptionPlan.Free));
    }

    [Fact]
    public async Task PlanChanges_NeedAPaidSubscription()
    {
        await _billing.GetOverviewAsync(Owner); // on the trial

        await Assert.ThrowsAsync<BusinessRuleException>(() => _billing.ChangePlanAsync(Owner, SubscriptionPlan.Starter));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _billing.CancelAsync(Owner));
    }

    [Fact]
    public async Task ARenewal_NeverShortensAccessThatWasExtended()
    {
        await SubscribeAsync(SubscriptionPlan.Starter);
        var subscription = await GetSubscriptionAsync();
        var extendedTo = subscription.AccessUntil!.Value.AddMonths(3);
        var renewal = _provider.CreateRenewal(subscription, paid: true);
        subscription.AccessUntil = extendedTo;
        await _context.SaveChangesAsync();

        await _billing.ApplyEventAsync(renewal, BillingSource.Fake);

        (await GetSubscriptionAsync()).AccessUntil.Should().Be(extendedTo);
    }

    [Fact]
    public async Task EventsForAReplacedSubscription_AreIgnored()
    {
        await SubscribeAsync(SubscriptionPlan.Starter);
        var stale = _provider.CreateRenewal(await GetSubscriptionAsync(), paid: false) with { ProviderSubscriptionId = "fake_sub_old" };

        (await _billing.ApplyEventAsync(stale, BillingSource.Fake)).Should().BeFalse();
        (await GetSubscriptionAsync()).Status.Should().Be(SubscriptionStatus.Active);
    }
}
