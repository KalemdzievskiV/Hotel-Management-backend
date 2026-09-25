using System.Security.Claims;
using FluentAssertions;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
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

public class SubscriptionAdminServiceTests
{
    private const string Owner = "owner";
    private const string SuperAdmin = "support";

    private readonly ApplicationDbContext _context;
    private readonly TestClock _clock = new();
    private readonly FakeBillingProvider _provider;
    private readonly BillingService _billing;
    private readonly SubscriptionAdminService _admin;

    public SubscriptionAdminServiceTests()
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
        _admin = new SubscriptionAdminService(_context, _billing, new[] { _provider }, _clock);

        _context.Roles.Add(new IdentityRole(AppRoles.Admin) { Id = AppRoles.Admin });
        _context.Users.Add(new ApplicationUser { Id = Owner, UserName = Owner, Email = "olga@lakeview.mk", FirstName = "Olga", LastName = "Owner", IsActive = true });
        _context.Users.Add(new ApplicationUser { Id = SuperAdmin, UserName = SuperAdmin, FirstName = "Sam", LastName = "Support" });
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = Owner, RoleId = AppRoles.Admin });
        _context.SaveChanges();
        entitlements.EnsureSubscriptionAsync(Owner).Wait(); // a 30-day trial
    }

    private DateTime Now => _clock.UtcDateTime;

    private Task<Subscription> GetSubscriptionAsync() =>
        _context.Subscriptions.Include(s => s.Events).SingleAsync(s => s.OwnerId == Owner);

    private async Task PayByCardAsync(SubscriptionPlan plan)
    {
        var session = await _billing.StartCheckoutAsync(Owner, plan, BillingInterval.Monthly);
        var (body, signature) = _provider.CompleteCheckout(_provider.ReadCheckout(session.CheckoutUrl.Split("session=")[1]));
        await _billing.HandleWebhookAsync("fake", body, signature);
    }

    private async Task FailRenewalAsync()
    {
        var (body, signature) = _provider.CreateWebhook(_provider.CreateRenewal(await GetSubscriptionAsync(), paid: false));
        await _billing.HandleWebhookAsync("fake", body, signature);
    }

    [Fact]
    public async Task ExtendingATrial_MovesItsEnd_AndRecordsWhoAndWhy()
    {
        var detail = await _admin.ExtendAsync(Owner, new ExtendSubscriptionRequest { Days = 14, Reason = "Asked at the trade fair" }, SuperAdmin);

        var subscription = await GetSubscriptionAsync();
        subscription.AccessUntil.Should().Be(Now.AddDays(PlanCatalog.TrialDays + 14));
        subscription.TrialEndsAt.Should().Be(subscription.AccessUntil);
        subscription.Status.Should().Be(SubscriptionStatus.Trialing);

        var entry = detail.Billing.History.First();
        entry.Type.Should().Be(SubscriptionEventType.TrialExtended);
        entry.Reason.Should().Be("Asked at the trade fair");
        entry.ActorName.Should().Be("Sam Support");
        entry.OldAccessUntil.Should().Be(Now.AddDays(PlanCatalog.TrialDays));
    }

    [Fact]
    public async Task ExtendingACardSubscription_PushesTheNextChargeWithoutCharging()
    {
        await PayByCardAsync(SubscriptionPlan.Starter);
        var paidUntil = (await GetSubscriptionAsync()).AccessUntil!.Value;

        await _admin.ExtendAsync(Owner, new ExtendSubscriptionRequest { Until = paidUntil.AddDays(10), Reason = "Outage compensation" }, SuperAdmin);

        // At the old renewal date nothing is charged
        _clock.Now = paidUntil.AddHours(1);
        await _billing.RunMaintenanceAsync();
        var subscription = await GetSubscriptionAsync();
        subscription.Events.Should().NotContain(e => e.Type == SubscriptionEventType.Renewed);
        subscription.Events.Should().ContainSingle(e => e.Type == SubscriptionEventType.Extended);

        // It renews at the new date, for a normal period from there
        _clock.Now = paidUntil.AddDays(10).AddHours(1);
        await _billing.RunMaintenanceAsync();
        (await GetSubscriptionAsync()).AccessUntil.Should().Be(paidUntil.AddDays(10).AddMonths(1));
    }

    [Fact]
    public async Task Extending_IsRefusedWhenItDoesntMakeSense()
    {
        var request = (int days) => new ExtendSubscriptionRequest { Days = days, Reason = "Because" };

        // Too far in one step, or not later than the current end
        await Assert.ThrowsAsync<BusinessRuleException>(() => _admin.ExtendAsync(Owner, request(400), SuperAdmin));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _admin.ExtendAsync(Owner,
            new ExtendSubscriptionRequest { Until = Now.AddDays(3), Reason = "Because" }, SuperAdmin));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _admin.ExtendAsync(Owner, new ExtendSubscriptionRequest { Reason = "No dates" }, SuperAdmin));

        // A failed payment gets grace, not an extension
        await PayByCardAsync(SubscriptionPlan.Starter);
        await FailRenewalAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => _admin.ExtendAsync(Owner, request(10), SuperAdmin));

        // Nothing to extend on Free
        _clock.Advance(TimeSpan.FromDays(60));
        await _billing.RunMaintenanceAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => _admin.ExtendAsync(Owner, request(10), SuperAdmin));
    }

    [Fact]
    public async Task ABankTransfer_GivesThePlanUntilTheDatePaidFor_AndFurtherPaymentsContinueFromThere()
    {
        var until = Now.AddMonths(3);
        await _admin.RecordManualPaymentAsync(Owner, new ManualPaymentRequest
        {
            Plan = SubscriptionPlan.Starter, Until = until, Amount = 36m, Reference = "MK07 2500 1234", Reason = "Paid invoice 2026-014"
        }, SuperAdmin);

        var subscription = await GetSubscriptionAsync();
        subscription.Plan.Should().Be(SubscriptionPlan.Starter);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.Source.Should().Be(BillingSource.Manual);
        subscription.AccessUntil.Should().Be(until);
        var payment = subscription.Events.Single(e => e.Type == SubscriptionEventType.ManualPayment);
        payment.Amount.Should().Be(36m);
        payment.Reference.Should().Be("MK07 2500 1234");

        // The next payment must reach past the current end
        await Assert.ThrowsAsync<BusinessRuleException>(() => _admin.RecordManualPaymentAsync(Owner, new ManualPaymentRequest
        {
            Plan = SubscriptionPlan.Starter, Until = until.AddDays(-1), Amount = 12m, Reason = "Invoice 015"
        }, SuperAdmin));
        await _admin.RecordManualPaymentAsync(Owner, new ManualPaymentRequest
        {
            Plan = SubscriptionPlan.Starter, Until = until.AddMonths(1), Amount = 12m, Reason = "Invoice 015"
        }, SuperAdmin);

        // Nothing renews bank transfers automatically; when the paid time runs out, the owner is on Free
        _clock.Now = until.AddMonths(1).AddHours(1);
        await _billing.RunMaintenanceAsync();
        subscription = await GetSubscriptionAsync();
        subscription.Plan.Should().Be(SubscriptionPlan.Free);
        subscription.Events.OrderBy(e => e.Id).Last().Reason.Should().Be("Paid period ended");
    }

    [Fact]
    public async Task ManualPaymentsAndFreeAccess_AreRefusedForOwnersPayingByCard()
    {
        await PayByCardAsync(SubscriptionPlan.Starter);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _admin.RecordManualPaymentAsync(Owner, new ManualPaymentRequest
        {
            Plan = SubscriptionPlan.Pro, Until = Now.AddMonths(2), Amount = 58m, Reason = "Would double charge"
        }, SuperAdmin));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _admin.GrantAccessAsync(Owner, new GrantAccessRequest
        {
            Plan = SubscriptionPlan.Pro, Until = Now.AddMonths(2), Reason = "Would double charge"
        }, SuperAdmin));
    }

    [Fact]
    public async Task FreeAccess_LastsUntilTheDate()
    {
        await _admin.GrantAccessAsync(Owner, new GrantAccessRequest { Plan = SubscriptionPlan.Pro, Until = Now.AddMonths(6), Reason = "Launch partner" }, SuperAdmin);

        var subscription = await GetSubscriptionAsync();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.Source.Should().Be(BillingSource.None);
        subscription.Events.Should().ContainSingle(e => e.Type == SubscriptionEventType.AccessGranted && e.ActorUserId == SuperAdmin);

        // Free access can itself be extended
        await _admin.ExtendAsync(Owner, new ExtendSubscriptionRequest { Days = 30, Reason = "Still a partner" }, SuperAdmin);

        _clock.Advance(TimeSpan.FromDays(250));
        await _billing.RunMaintenanceAsync();
        subscription = await GetSubscriptionAsync();
        subscription.Plan.Should().Be(SubscriptionPlan.Free);
        subscription.Events.OrderBy(e => e.Id).Last().Reason.Should().Be("Free access ended");
    }

    [Fact]
    public async Task ChangingThePlan_AppliesNow_AndCardPayersAreChargedTheNewPriceFromRenewal()
    {
        await PayByCardAsync(SubscriptionPlan.Starter);

        await _admin.ChangePlanAsync(Owner, new AdminChangePlanRequest { Plan = SubscriptionPlan.Pro, Reason = "Opened a second hotel" }, SuperAdmin);
        (await GetSubscriptionAsync()).Plan.Should().Be(SubscriptionPlan.Pro);

        _clock.Advance(TimeSpan.FromDays(31));
        await _billing.RunMaintenanceAsync();
        (await GetSubscriptionAsync()).Events.Single(e => e.Type == SubscriptionEventType.Renewed).Amount.Should().Be(29m);
    }

    [Fact]
    public async Task Grace_IsForFailedPayments_AndAddsToTheCurrentGrace()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _admin.GrantGraceAsync(Owner, new GrantGraceRequest { Days = 7, Reason = "Not due" }, SuperAdmin));

        await PayByCardAsync(SubscriptionPlan.Starter);
        await FailRenewalAsync();
        var graceUntil = (await GetSubscriptionAsync()).GraceUntil!.Value;

        await _admin.GrantGraceAsync(Owner, new GrantGraceRequest { Days = 7, Reason = "Bank card replaced" }, SuperAdmin);

        (await GetSubscriptionAsync()).GraceUntil.Should().Be(graceUntil.AddDays(7));
    }

    [Fact]
    public async Task TheList_FiltersAndCountsRevenue()
    {
        (await _admin.ListAsync(SubscriptionFilter.Trialing, null)).Should().ContainSingle();
        (await _admin.ListAsync(SubscriptionFilter.TrialEndingSoon, null)).Should().BeEmpty();
        (await _admin.ListAsync(SubscriptionFilter.All, "lakeview")).Should().ContainSingle();
        (await _admin.ListAsync(SubscriptionFilter.All, "nobody")).Should().BeEmpty();

        await PayByCardAsync(SubscriptionPlan.Pro);

        var paying = (await _admin.ListAsync(SubscriptionFilter.Paying, null)).Single();
        paying.MonthlyRevenue.Should().Be(29m);
        paying.OwnerName.Should().Be("Olga Owner");

        var stats = await _admin.GetStatsAsync();
        stats.Paying.Should().Be(1);
        stats.Trialing.Should().Be(0);
        stats.MonthlyRevenue.Should().Be(29m);
    }

    [Fact]
    public async Task UsersWithoutASubscription_AreNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _admin.GetAsync(SuperAdmin));
        (await _context.Subscriptions.CountAsync()).Should().Be(1);
    }
}
