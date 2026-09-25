using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using HotelManagement.Controllers;
using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Owners sign up themselves, and what they can add depends on their plan
/// </summary>
public class PlanLimitsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestApi _api;

    public PlanLimitsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _api = new TestApi(factory.CreateClient());
    }

    private record Owner(string Token, string Id, string Email);

    private async Task<Owner> SignUpOwnerAsync()
    {
        var email = $"owner{Guid.NewGuid():N}@test.com";
        var response = await _api.Client.PostAsJsonAsync("/api/Auth/register-owner", new RegisterOwnerRequestDto
        {
            FirstName = "Olga",
            LastName = "Owner",
            Email = email,
            Password = "Passw0rd"
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
        return new Owner(auth.Token, await _api.GetUserIdAsync(email), email);
    }

    private async Task PutOnPlanAsync(Owner owner, SubscriptionPlan plan)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subscription = await context.Subscriptions.SingleAsync(s => s.OwnerId == owner.Id);
        subscription.Plan = plan;
        subscription.Status = SubscriptionStatus.Active;
        subscription.AccessUntil = plan == SubscriptionPlan.Free ? null : DateTime.UtcNow.AddMonths(1);
        await context.SaveChangesAsync();
    }

    private Task<HotelDto> CreateHotelAsync(Owner owner) =>
        _api.PostAsync<HotelDto>("/api/Hotels", owner.Token, new HotelDto
        {
            Name = $"Hotel {Guid.NewGuid():N}",
            Address = "1 Main St",
            City = "Ohrid",
            Country = "North Macedonia"
        });

    private Task<HttpResponseMessage> AddRoomAsync(string token, int hotelId, string number) =>
        _api.SendAsync(HttpMethod.Post, "/api/Rooms", token, new RoomDto
        {
            HotelId = hotelId,
            RoomNumber = number,
            Type = RoomType.Double,
            Capacity = 2,
            PricePerNight = 50
        });

    private Task<HttpResponseMessage> AddStaffAsync(Owner owner, int hotelId, string role = "Housekeeper") =>
        _api.SendAsync(HttpMethod.Post, "/api/Staff", owner.Token, new CreateStaffDto
        {
            FirstName = "Hana",
            LastName = "Housekeeper",
            Email = $"staff{Guid.NewGuid():N}@test.com",
            Password = "Passw0rd",
            Role = role,
            HotelId = hotelId
        });

    /// <summary>
    /// A refusal because of the plan: 402 with what ran out and what to upgrade to
    /// </summary>
    private static async Task ShouldBePlanLimitAsync(HttpResponseMessage response, string limit, string? upgradeTo)
    {
        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        data.GetProperty("code").GetString().Should().Be("plan_limit");
        data.GetProperty("limit").GetString().Should().Be(limit);
        if (upgradeTo == null)
            data.GetProperty("upgradeTo").ValueKind.Should().Be(JsonValueKind.Null);
        else
            data.GetProperty("upgradeTo").GetString().Should().Be(upgradeTo);
    }

    [Fact]
    public async Task ANewOwner_SignsUpAsAnAdminOnATrialOfTheTopPlan()
    {
        var owner = await SignUpOwnerAsync();

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subscription = await context.Subscriptions.Include(s => s.Events).SingleAsync(s => s.OwnerId == owner.Id);
        subscription.Plan.Should().Be(SubscriptionPlan.Pro);
        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
        subscription.AccessUntil.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
        subscription.Events.Should().ContainSingle(e => e.Type == SubscriptionEventType.TrialStarted);

        // The trial allows several hotels
        await CreateHotelAsync(owner);
        await CreateHotelAsync(owner);
    }

    [Fact]
    public async Task OwnerSignUp_RefusesAnEmailThatIsTaken()
    {
        var owner = await SignUpOwnerAsync();

        var again = await _api.Client.PostAsJsonAsync("/api/Auth/register-owner", new RegisterOwnerRequestDto
        {
            FirstName = "Other",
            LastName = "Person",
            Email = owner.Email,
            Password = "Passw0rd"
        });

        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NamesInAnyAlphabet_AreAccepted()
    {
        var response = await _api.Client.PostAsJsonAsync("/api/Auth/register-owner", new RegisterOwnerRequestDto
        {
            FirstName = "Владимир",
            LastName = "Калемџиевски",
            Email = $"cyrillic{Guid.NewGuid():N}@test.com",
            Password = "Passw0rd"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var hotel = await _api.CreateHotelAsync();
        var guest = await _api.SendAsync(HttpMethod.Post, "/api/Guests", hotel.AdminToken, new GuestDto
        {
            HotelId = hotel.HotelId,
            FirstName = "Ана",
            LastName = "Kalemdžievska",
            Email = $"g{Guid.NewGuid():N}@test.com",
            PhoneNumber = "+389 70 123 456"
        });
        guest.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task FreePlan_AllowsFiveRooms_AndSaysWhatToUpgradeTo()
    {
        var owner = await SignUpOwnerAsync();
        var hotel = await CreateHotelAsync(owner);
        await PutOnPlanAsync(owner, SubscriptionPlan.Free);

        for (var i = 1; i <= 5; i++)
            (await AddRoomAsync(owner.Token, hotel.Id, $"10{i}")).StatusCode.Should().Be(HttpStatusCode.Created);

        await ShouldBePlanLimitAsync(await AddRoomAsync(owner.Token, hotel.Id, "106"), "rooms", "Starter");

        // The limit belongs to the owner, so it applies to their managers too
        var managerToken = await TestAuth.GetTokenAsync(_api.Client, "Manager", hotelId: hotel.Id);
        await ShouldBePlanLimitAsync(await AddRoomAsync(managerToken, hotel.Id, "107"), "rooms", "Starter");

        // Upgrading lifts it straight away
        await PutOnPlanAsync(owner, SubscriptionPlan.Starter);
        (await AddRoomAsync(owner.Token, hotel.Id, "106")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task FreePlan_HasOneHotel()
    {
        var owner = await SignUpOwnerAsync();
        await PutOnPlanAsync(owner, SubscriptionPlan.Free);
        await CreateHotelAsync(owner);

        var second = await _api.SendAsync(HttpMethod.Post, "/api/Hotels", owner.Token, new HotelDto
        {
            Name = "Second", Address = "2 Main St", City = "Skopje", Country = "North Macedonia"
        });

        await ShouldBePlanLimitAsync(second, "hotels", "Pro");
    }

    [Fact]
    public async Task SuperAdmins_CanGoOverACustomersLimitWhenHelpingThem()
    {
        var owner = await SignUpOwnerAsync();
        var hotel = await CreateHotelAsync(owner);
        await PutOnPlanAsync(owner, SubscriptionPlan.Free);
        for (var i = 1; i <= 5; i++)
            await AddRoomAsync(owner.Token, hotel.Id, $"20{i}");

        var superAdminToken = await TestAuth.GetTokenAsync(_api.Client, "SuperAdmin");
        (await AddRoomAsync(superAdminToken, hotel.Id, "206")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task FreePlan_CanViewButNotChangeInventory()
    {
        var owner = await SignUpOwnerAsync();
        var hotel = await CreateHotelAsync(owner);

        // Stock added during the trial stays visible afterwards
        var item = await _api.PostAsync<InventoryItemDto>("/api/Inventory", owner.Token, new CreateInventoryItemDto
        {
            HotelId = hotel.Id, Name = "Towels", Category = InventoryCategory.Towels, Quantity = 10
        });
        await PutOnPlanAsync(owner, SubscriptionPlan.Free);

        (await _api.SendAsync(HttpMethod.Get, $"/api/Inventory/hotel/{hotel.Id}", owner.Token))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await ShouldBePlanLimitAsync(await _api.SendAsync(HttpMethod.Post, "/api/Inventory", owner.Token, new CreateInventoryItemDto
        {
            HotelId = hotel.Id, Name = "Soap", Category = InventoryCategory.Amenities
        }), "inventory", "Starter");
        await ShouldBePlanLimitAsync(await _api.SendAsync(HttpMethod.Post, "/api/Inventory/transactions", owner.Token, new CreateInventoryTransactionDto
        {
            InventoryItemId = item.Id, Type = InventoryTransactionType.Usage, Quantity = 1
        }), "inventory", "Starter");
        await ShouldBePlanLimitAsync(await _api.SendAsync(HttpMethod.Delete, $"/api/Inventory/{item.Id}", owner.Token), "inventory", "Starter");
    }

    [Fact]
    public async Task FreePlanReports_OnlyReachBackThirtyDays()
    {
        var owner = await SignUpOwnerAsync();
        var hotel = await CreateHotelAsync(owner);
        var room = await (await AddRoomAsync(owner.Token, hotel.Id, "301")).Content.ReadFromJsonAsync<RoomDto>();

        // A finished stay from two months ago
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var guest = new Guest { FirstName = "Old", LastName = "Stay", Email = $"old{Guid.NewGuid():N}@test.com", PhoneNumber = "1", HotelId = hotel.Id };
            context.Guests.Add(guest);
            await context.SaveChangesAsync();
            context.Reservations.Add(new Reservation
            {
                HotelId = hotel.Id, RoomId = room!.Id, GuestId = guest.Id, CreatedByUserId = owner.Id,
                CheckInDate = DateTime.UtcNow.Date.AddDays(-60), CheckOutDate = DateTime.UtcNow.Date.AddDays(-58),
                Status = ReservationStatus.CheckedOut, TotalAmount = 100
            });
            await context.SaveChangesAsync();
        }

        var url = $"/api/Reports/revenue/daily?startDate={DateTime.UtcNow.AddDays(-90):yyyy-MM-dd}&endDate={DateTime.UtcNow:yyyy-MM-dd}";
        (await _api.GetAsync<List<DailyRevenueDto>>(url, owner.Token)).Should().ContainSingle();

        await PutOnPlanAsync(owner, SubscriptionPlan.Free);
        (await _api.GetAsync<List<DailyRevenueDto>>(url, owner.Token)).Should().BeEmpty();
    }

    [Fact]
    public async Task Owners_ManageTheirOwnStaffWithinTheirPlan()
    {
        var owner = await SignUpOwnerAsync();
        var hotel = await CreateHotelAsync(owner);
        await PutOnPlanAsync(owner, SubscriptionPlan.Free);

        var created = await AddStaffAsync(owner, hotel.Id);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var housekeeper = (await created.Content.ReadFromJsonAsync<UserDto>())!;
        housekeeper.Roles.Should().Equal("Housekeeper");
        housekeeper.HotelId.Should().Be(hotel.Id);

        await ShouldBePlanLimitAsync(await AddStaffAsync(owner, hotel.Id, "Manager"), "staff", "Starter");

        // Deactivating frees the place; reactivating needs it back
        (await _api.SendAsync(HttpMethod.Post, $"/api/Staff/{housekeeper.Id}/deactivate", owner.Token)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AddStaffAsync(owner, hotel.Id, "Manager")).StatusCode.Should().Be(HttpStatusCode.Created);
        await ShouldBePlanLimitAsync(await _api.SendAsync(HttpMethod.Post, $"/api/Staff/{housekeeper.Id}/activate", owner.Token), "staff", "Starter");

        var staff = await _api.GetAsync<List<UserDto>>("/api/Staff", owner.Token);
        staff.Select(s => s.Roles.Single()).Should().BeEquivalentTo("Housekeeper", "Manager");
    }

    [Fact]
    public async Task Owners_CannotTouchOtherOwnersStaff()
    {
        var owner = await SignUpOwnerAsync();
        var other = await SignUpOwnerAsync();
        var hotel = await CreateHotelAsync(owner);
        var otherHotel = await CreateHotelAsync(other);
        var otherStaff = (await (await AddStaffAsync(other, otherHotel.Id)).Content.ReadFromJsonAsync<UserDto>())!;

        (await AddStaffAsync(owner, otherHotel.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Post, $"/api/Staff/{otherStaff.Id}/deactivate", owner.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.SendAsync(HttpMethod.Patch, $"/api/Staff/{otherStaff.Id}/hotel", owner.Token, new AssignHotelRequest { HotelId = hotel.Id }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.GetAsync<List<UserDto>>("/api/Staff", owner.Token)).Should().BeEmpty();

        // Only managers and housekeepers can be added this way
        (await AddStaffAsync(owner, hotel.Id, "Admin")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Managers don't manage staff
        var managerToken = await TestAuth.GetTokenAsync(_api.Client, "Manager", hotelId: hotel.Id);
        (await _api.SendAsync(HttpMethod.Get, "/api/Staff", managerToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StaffCanBeMovedBetweenTheOwnersHotels()
    {
        var owner = await SignUpOwnerAsync();
        var first = await CreateHotelAsync(owner);
        var second = await CreateHotelAsync(owner);
        var staff = (await (await AddStaffAsync(owner, first.Id)).Content.ReadFromJsonAsync<UserDto>())!;

        (await _api.SendAsync(HttpMethod.Patch, $"/api/Staff/{staff.Id}/hotel", owner.Token, new AssignHotelRequest { HotelId = second.Id }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await _api.GetAsync<List<UserDto>>("/api/Staff", owner.Token)).Single().HotelId.Should().Be(second.Id);
    }
}
