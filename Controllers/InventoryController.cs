using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HotelManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.Admin},{AppRoles.Manager}")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly IHotelAccessService _hotelAccess;
    private readonly IEntitlementService _entitlements;

    public InventoryController(IInventoryService inventoryService, IHotelAccessService hotelAccess, IEntitlementService entitlements)
    {
        _inventoryService = inventoryService;
        _hotelAccess = hotelAccess;
        _entitlements = entitlements;
    }

    /// <summary>
    /// Stock can be viewed on any plan; changing it needs a plan with Inventory
    /// </summary>
    private async Task<bool> CanChangeItemAsync(int itemId)
    {
        var item = await _inventoryService.GetItemByIdAsync(itemId);
        if (item == null || !await _hotelAccess.CanAccessHotelAsync(item.HotelId))
            return false;

        await _entitlements.EnsureFeatureAsync(item.HotelId, PlanFeature.Inventory);
        return true;
    }

    [HttpGet("hotel/{hotelId}")]
    public async Task<IActionResult> GetByHotel(int hotelId, [FromQuery] bool includeInactive = false)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _inventoryService.GetItemsByHotelAsync(hotelId, includeInactive));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _inventoryService.GetItemByIdAsync(id);
        if (item == null || !await _hotelAccess.CanAccessHotelAsync(item.HotelId))
            return NotFound();

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInventoryItemDto dto)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(dto.HotelId))
            return Forbid();

        await _entitlements.EnsureFeatureAsync(dto.HotelId, PlanFeature.Inventory);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await _inventoryService.CreateItemAsync(dto, userId);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInventoryItemDto dto)
    {
        if (!await CanChangeItemAsync(id))
            return NotFound();

        return Ok(await _inventoryService.UpdateItemAsync(id, dto));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await CanChangeItemAsync(id))
            return NotFound();

        await _inventoryService.DeleteItemAsync(id);
        return NoContent();
    }

    [HttpGet("hotel/{hotelId}/low-stock")]
    public async Task<IActionResult> GetLowStock(int hotelId)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _inventoryService.GetLowStockItemsAsync(hotelId));
    }

    [HttpGet("hotel/{hotelId}/transactions")]
    public async Task<IActionResult> GetTransactions(int hotelId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _inventoryService.GetTransactionsAsync(hotelId, from, to));
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> RecordTransaction([FromBody] CreateInventoryTransactionDto dto)
    {
        var item = await _inventoryService.GetItemByIdAsync(dto.InventoryItemId);
        if (item == null || !await _hotelAccess.CanAccessHotelAsync(item.HotelId))
            return NotFound();

        await _entitlements.EnsureFeatureAsync(item.HotelId, PlanFeature.Inventory);

        // A transaction tied to a room must use a room of the item's hotel
        if (dto.RoomId.HasValue && await _hotelAccess.GetRoomHotelIdAsync(dto.RoomId.Value) != item.HotelId)
            return BadRequest(new { message = "Room does not belong to the item's hotel" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _inventoryService.RecordTransactionAsync(dto, userId));
    }

    [HttpGet("hotel/{hotelId}/cost-analysis")]
    public async Task<IActionResult> GetCostAnalysis(int hotelId)
    {
        if (!await _hotelAccess.CanAccessHotelAsync(hotelId))
            return Forbid();

        return Ok(await _inventoryService.GetCostAnalysisAsync(hotelId));
    }
}
