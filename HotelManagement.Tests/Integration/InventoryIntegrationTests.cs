using System.Net;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;
using HotelManagement.Tests.Helpers;
using Xunit;

namespace HotelManagement.Tests.Integration;

/// <summary>
/// Stock levels follow the transactions recorded against them, per hotel
/// </summary>
public class InventoryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly TestApi _api;

    public InventoryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _api = new TestApi(factory.CreateClient());
    }

    private Task<InventoryItemDto> CreateItemAsync(TestApi.Hotel hotel, int quantity = 10) =>
        _api.PostAsync<InventoryItemDto>("/api/Inventory", hotel.AdminToken, new CreateInventoryItemDto
        {
            HotelId = hotel.HotelId,
            Name = "Bath towel",
            Category = InventoryCategory.Towels,
            Quantity = quantity,
            MinimumThreshold = 5,
            UnitCost = 4
        });

    private Task<HttpResponseMessage> RecordAsync(string token, int itemId, InventoryTransactionType type, int quantity, int? roomId = null) =>
        _api.SendAsync(HttpMethod.Post, "/api/Inventory/transactions", token, new CreateInventoryTransactionDto
        {
            InventoryItemId = itemId,
            Type = type,
            Quantity = quantity,
            RoomId = roomId
        });

    [Fact]
    public async Task UsageAndRestock_MoveTheStockAndTheLowStockList()
    {
        var hotel = await _api.CreateHotelAsync();
        var item = await CreateItemAsync(hotel, quantity: 10);

        (await RecordAsync(hotel.AdminToken, item.Id, InventoryTransactionType.Usage, 6, hotel.RoomId)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _api.GetAsync<List<LowStockAlertDto>>($"/api/Inventory/hotel/{hotel.HotelId}/low-stock", hotel.AdminToken))
            .Should().ContainSingle(i => i.ItemId == item.Id && i.CurrentQuantity == 4);

        (await RecordAsync(hotel.AdminToken, item.Id, InventoryTransactionType.Restock, 20)).StatusCode.Should().Be(HttpStatusCode.OK);
        var restocked = await _api.GetAsync<InventoryItemDto>($"/api/Inventory/{item.Id}", hotel.AdminToken);
        restocked.Quantity.Should().Be(24);
        restocked.LastRestocked.Should().NotBeNull();
        (await _api.GetAsync<List<LowStockAlertDto>>($"/api/Inventory/hotel/{hotel.HotelId}/low-stock", hotel.AdminToken))
            .Should().BeEmpty();

        (await _api.GetAsync<List<InventoryTransactionDto>>($"/api/Inventory/hotel/{hotel.HotelId}/transactions", hotel.AdminToken))
            .Should().HaveCount(2);
    }

    [Theory]
    [InlineData(InventoryTransactionType.Usage)]
    [InlineData(InventoryTransactionType.Damage)]
    [InlineData(InventoryTransactionType.Loss)]
    public async Task TakingOutMoreThanIsInStock_IsRefusedAndNothingIsRecorded(InventoryTransactionType type)
    {
        var hotel = await _api.CreateHotelAsync();
        var item = await CreateItemAsync(hotel, quantity: 5);

        (await RecordAsync(hotel.AdminToken, item.Id, type, 6)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _api.GetAsync<InventoryItemDto>($"/api/Inventory/{item.Id}", hotel.AdminToken)).Quantity.Should().Be(5);
        (await _api.GetAsync<List<InventoryTransactionDto>>($"/api/Inventory/hotel/{hotel.HotelId}/transactions", hotel.AdminToken))
            .Should().BeEmpty();

        // Using up exactly what's left is fine
        (await RecordAsync(hotel.AdminToken, item.Id, type, 5)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _api.GetAsync<InventoryItemDto>($"/api/Inventory/{item.Id}", hotel.AdminToken)).Quantity.Should().Be(0);
    }

    [Fact]
    public async Task OtherHotelsStaff_CannotSeeOrChangeThisStock()
    {
        var hotel = await _api.CreateHotelAsync();
        var other = await _api.CreateHotelAsync();
        var item = await CreateItemAsync(hotel);

        (await _api.SendAsync(HttpMethod.Get, $"/api/Inventory/{item.Id}", other.AdminToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.SendAsync(HttpMethod.Get, $"/api/Inventory/hotel/{hotel.HotelId}", other.AdminToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RecordAsync(other.AdminToken, item.Id, InventoryTransactionType.Usage, 1)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _api.SendAsync(HttpMethod.Delete, $"/api/Inventory/{item.Id}", other.AdminToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Nor charge this hotel's stock to their own room
        (await RecordAsync(hotel.AdminToken, item.Id, InventoryTransactionType.Usage, 1, other.RoomId)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HousekeepersAndGuests_HaveNoAccessToInventory()
    {
        var hotel = await _api.CreateHotelAsync();
        var housekeeperToken = await TestAuth.GetTokenAsync(_api.Client, "Housekeeper", hotelId: hotel.HotelId);
        var guestToken = await TestAuth.GetTokenAsync(_api.Client, "Guest");

        (await _api.SendAsync(HttpMethod.Get, $"/api/Inventory/hotel/{hotel.HotelId}", housekeeperToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _api.SendAsync(HttpMethod.Get, $"/api/Inventory/hotel/{hotel.HotelId}", guestToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
