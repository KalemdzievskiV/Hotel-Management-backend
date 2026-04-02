using HotelManagement.Models.DTOs;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HotelManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin,Manager")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("hotel/{hotelId}")]
    public async Task<IActionResult> GetByHotel(int hotelId, [FromQuery] bool includeInactive = false)
    {
        var items = await _inventoryService.GetItemsByHotelAsync(hotelId, includeInactive);
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _inventoryService.GetItemByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInventoryItemDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await _inventoryService.CreateItemAsync(dto, userId);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInventoryItemDto dto)
    {
        var item = await _inventoryService.UpdateItemAsync(id, dto);
        return Ok(item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _inventoryService.DeleteItemAsync(id);
        return NoContent();
    }

    [HttpGet("hotel/{hotelId}/low-stock")]
    public async Task<IActionResult> GetLowStock(int hotelId)
    {
        var alerts = await _inventoryService.GetLowStockItemsAsync(hotelId);
        return Ok(alerts);
    }

    [HttpGet("hotel/{hotelId}/transactions")]
    public async Task<IActionResult> GetTransactions(int hotelId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var transactions = await _inventoryService.GetTransactionsAsync(hotelId, from, to);
        return Ok(transactions);
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> RecordTransaction([FromBody] CreateInventoryTransactionDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var transaction = await _inventoryService.RecordTransactionAsync(dto, userId);
        return Ok(transaction);
    }

    [HttpGet("hotel/{hotelId}/cost-analysis")]
    public async Task<IActionResult> GetCostAnalysis(int hotelId)
    {
        var analysis = await _inventoryService.GetCostAnalysisAsync(hotelId);
        return Ok(analysis);
    }
}
