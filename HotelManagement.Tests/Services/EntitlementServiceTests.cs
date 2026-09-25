using System.Security.Claims;
using FluentAssertions;
using HotelManagement.Data;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Models.Constants;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Implementations;
using HotelManagement.Services.Interfaces;
using HotelManagement.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Services;

public class EntitlementServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly TestClock _clock = new();
    private readonly Mock<IHttpContextAccessor> _httpContext = new();
    private readonly EntitlementService _service;
    private int _nextRoom;

    private const string Owner = "owner";

    public EntitlementServiceTests()
    {
        _context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        _service = new EntitlementService(_context, _httpContext.Object, _clock);
        ActAs(AppRoles.Admin);

        foreach (var role in AppRoles.AllRoles)
            _context.Roles.Add(new IdentityRole(role) { Id = role });
        AddUser(Owner, AppRoles.Admin);
        _context.SaveChanges();
    }

    private void ActAs(string role) =>
        _httpContext.Setup(x => x.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "Test"))
        });

    private void AddUser(string id, string role, int? hotelId = null, bool active = true)
    {
        _context.Users.Add(new ApplicationUser { Id = id, UserName = id, FirstName = id, LastName = "X", HotelId = hotelId, IsActive = active });
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = role });
    }

    private int AddHotel(string ownerId = Owner, int rooms = 0)
    {
        var hotel = new Hotel { Name = "H", OwnerId = ownerId, Address = "A", City = "C", Country = "X" };
        _context.Hotels.Add(hotel);
        _context.SaveChanges();
        AddRooms(hotel.Id, rooms);
        return hotel.Id;
    }

    private void AddRooms(int hotelId, int count)
    {
        for (var i = 0; i < count; i++)
            _context.Rooms.Add(new Room { HotelId = hotelId, RoomNumber = $"R{++_nextRoom}", Capacity = 2, PricePerNight = 100 });
        _context.SaveChanges();
    }

    private async Task PutOwnerOnAsync(SubscriptionPlan plan)
    {
        var subscription = await _service.EnsureSubscriptionAsync(Owner);
        subscription.Plan = plan;
        subscription.Status = SubscriptionStatus.Active;
        subscription.AccessUntil = plan == SubscriptionPlan.Free ? null : _clock.UtcDateTime.AddMonths(1);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task NewOwners_StartATrialOfTheTopPlan_OnlyOnce()
    {
        var subscription = await _service.EnsureSubscriptionAsync(Owner);

        subscription.Plan.Should().Be(SubscriptionPlan.Pro);
        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
        subscription.AccessUntil.Should().Be(_clock.UtcDateTime.AddDays(PlanCatalog.TrialDays));
        (await _context.SubscriptionEvents.SingleAsync()).Type.Should().Be(SubscriptionEventType.TrialStarted);

        (await _service.EnsureSubscriptionAsync(Owner)).Id.Should().Be(subscription.Id);
        (await _context.Subscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ATrial_StopsCountingTheMomentItEnds()
    {
        await _service.EnsureSubscriptionAsync(Owner);
        (await _service.GetPlanForOwnerAsync(Owner)).Plan.Should().Be(SubscriptionPlan.Pro);

        _clock.Advance(TimeSpan.FromDays(PlanCatalog.TrialDays) + TimeSpan.FromMinutes(1));

        (await _service.GetPlanForOwnerAsync(Owner)).Plan.Should().Be(SubscriptionPlan.Free);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active, 1, null, SubscriptionPlan.Starter)]
    [InlineData(SubscriptionStatus.Active, -1, null, SubscriptionPlan.Free)]
    [InlineData(SubscriptionStatus.PastDue, -1, 3, SubscriptionPlan.Starter)]
    [InlineData(SubscriptionStatus.PastDue, -1, -1, SubscriptionPlan.Free)]
    [InlineData(SubscriptionStatus.Expired, 5, null, SubscriptionPlan.Free)]
    public void EffectivePlan_FollowsStatusAndDates(SubscriptionStatus status, int accessDays, int? graceDays, SubscriptionPlan expected)
    {
        var now = _clock.UtcDateTime;
        var subscription = new Subscription
        {
            Plan = SubscriptionPlan.Starter,
            Status = status,
            AccessUntil = now.AddDays(accessDays),
            GraceUntil = graceDays.HasValue ? now.AddDays(graceDays.Value) : null
        };

        subscription.GetEffectivePlan(now).Should().Be(expected);
    }

    [Fact]
    public async Task FreePlan_AllowsFiveRooms_ThenSuggestsStarter()
    {
        await PutOwnerOnAsync(SubscriptionPlan.Free);
        var hotelId = AddHotel(rooms: 4);

        await _service.EnsureCanAddRoomAsync(hotelId);
        AddRooms(hotelId, 1);

        var refused = await Assert.ThrowsAsync<PlanLimitException>(() => _service.EnsureCanAddRoomAsync(hotelId));
        refused.Limit.Should().Be("rooms");
        refused.Allowed.Should().Be(5);
        refused.UpgradeTo.Should().Be(SubscriptionPlan.Starter);
    }

    [Fact]
    public async Task RoomLimits_CountEveryHotelTheOwnerHas()
    {
        await PutOwnerOnAsync(SubscriptionPlan.Pro);
        AddHotel(rooms: 40);
        var second = AddHotel(rooms: 20);

        var refused = await Assert.ThrowsAsync<PlanLimitException>(() => _service.EnsureCanAddRoomAsync(second));
        refused.UpgradeTo.Should().BeNull(); // nothing bigger than Pro
    }

    [Fact]
    public async Task HotelLimits_StarterHasOne_ProHasThree()
    {
        await PutOwnerOnAsync(SubscriptionPlan.Starter);
        AddHotel();

        (await Assert.ThrowsAsync<PlanLimitException>(() => _service.EnsureCanAddHotelAsync(Owner)))
            .UpgradeTo.Should().Be(SubscriptionPlan.Pro);

        await PutOwnerOnAsync(SubscriptionPlan.Pro);
        await _service.EnsureCanAddHotelAsync(Owner);
    }

    [Fact]
    public async Task StaffLimits_CountOnlyActiveManagersAndHousekeepersOfTheOwnersHotels()
    {
        await PutOwnerOnAsync(SubscriptionPlan.Free);
        var hotelId = AddHotel();
        var otherHotel = AddHotel("someone-else");
        AddUser("former", AppRoles.Housekeeper, hotelId, active: false);
        AddUser("elsewhere", AppRoles.Manager, otherHotel);
        AddUser("guest-with-hotel", AppRoles.Guest, hotelId);
        await _context.SaveChangesAsync();

        await _service.EnsureCanAddStaffAsync(hotelId);

        AddUser("housekeeper", AppRoles.Housekeeper, hotelId);
        await _context.SaveChangesAsync();
        (await _service.GetUsageAsync(Owner)).Staff.Should().Be(1);
        (await Assert.ThrowsAsync<PlanLimitException>(() => _service.EnsureCanAddStaffAsync(hotelId)))
            .Limit.Should().Be("staff");
    }

    [Fact]
    public async Task Inventory_NeedsStarterOrAbove()
    {
        var hotelId = AddHotel();
        await PutOwnerOnAsync(SubscriptionPlan.Free);

        var refused = await Assert.ThrowsAsync<PlanLimitException>(() => _service.EnsureFeatureAsync(hotelId, PlanFeature.Inventory));
        refused.Limit.Should().Be("inventory");
        refused.UpgradeTo.Should().Be(SubscriptionPlan.Starter);

        await PutOwnerOnAsync(SubscriptionPlan.Starter);
        await _service.EnsureFeatureAsync(hotelId, PlanFeature.Inventory);
    }

    [Fact]
    public async Task Reports_OnTheFreePlanOnlyReachBackThirtyDays()
    {
        var hotelId = AddHotel();
        await PutOwnerOnAsync(SubscriptionPlan.Free);
        (await _service.GetReportHistoryStartAsync(new[] { hotelId }))
            .Should().Be(_clock.UtcDateTime.Date.AddDays(-PlanCatalog.LimitedReportDays));

        await PutOwnerOnAsync(SubscriptionPlan.Starter);
        (await _service.GetReportHistoryStartAsync(new[] { hotelId })).Should().BeNull();
    }

    [Fact]
    public async Task SuperAdmins_AreNotLimited()
    {
        await PutOwnerOnAsync(SubscriptionPlan.Free);
        var hotelId = AddHotel(rooms: 5);

        // Supporting a customer
        ActAs(AppRoles.SuperAdmin);
        await _service.EnsureCanAddRoomAsync(hotelId);
        await _service.EnsureFeatureAsync(hotelId, PlanFeature.Inventory);

        // A hotel the SuperAdmin owns, even when an Admin works in it
        ActAs(AppRoles.Admin);
        AddUser("platform", AppRoles.SuperAdmin);
        await _context.SaveChangesAsync();
        var platformHotel = AddHotel("platform", rooms: 100);
        await _service.EnsureCanAddRoomAsync(platformHotel);
    }
}
