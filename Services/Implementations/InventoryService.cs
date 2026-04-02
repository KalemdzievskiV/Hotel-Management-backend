using HotelManagement.Data;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Entities;
using HotelManagement.Models.Enums;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Implementations;

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _context;

    public InventoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<InventoryItemDto>> GetItemsByHotelAsync(int hotelId, bool includeInactive = false)
    {
        var query = _context.InventoryItems
            .Include(i => i.Hotel)
            .Where(i => i.HotelId == hotelId);

        if (!includeInactive)
            query = query.Where(i => i.IsActive);

        return await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .Select(i => MapToDto(i))
            .ToListAsync();
    }

    public async Task<InventoryItemDto?> GetItemByIdAsync(int id)
    {
        var item = await _context.InventoryItems
            .Include(i => i.Hotel)
            .FirstOrDefaultAsync(i => i.Id == id);

        return item == null ? null : MapToDto(item);
    }

    public async Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemDto dto, string userId)
    {
        var item = new InventoryItem
        {
            HotelId = dto.HotelId,
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            Quantity = dto.Quantity,
            MinimumThreshold = dto.MinimumThreshold,
            UnitCost = dto.UnitCost,
            Supplier = dto.Supplier,
            Unit = dto.Unit ?? "pcs",
            CreatedAt = DateTime.UtcNow
        };

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        await _context.Entry(item).Reference(i => i.Hotel).LoadAsync();
        return MapToDto(item);
    }

    public async Task<InventoryItemDto> UpdateItemAsync(int id, UpdateInventoryItemDto dto)
    {
        var item = await _context.InventoryItems
            .Include(i => i.Hotel)
            .FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new KeyNotFoundException($"Inventory item {id} not found");

        if (dto.Name != null) item.Name = dto.Name;
        if (dto.Description != null) item.Description = dto.Description;
        if (dto.Category.HasValue) item.Category = dto.Category.Value;
        if (dto.MinimumThreshold.HasValue) item.MinimumThreshold = dto.MinimumThreshold.Value;
        if (dto.UnitCost.HasValue) item.UnitCost = dto.UnitCost.Value;
        if (dto.Supplier != null) item.Supplier = dto.Supplier;
        if (dto.Unit != null) item.Unit = dto.Unit;
        if (dto.IsActive.HasValue) item.IsActive = dto.IsActive.Value;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(item);
    }

    public async Task DeleteItemAsync(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id)
            ?? throw new KeyNotFoundException($"Inventory item {id} not found");

        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<LowStockAlertDto>> GetLowStockItemsAsync(int hotelId)
    {
        return await _context.InventoryItems
            .Where(i => i.HotelId == hotelId && i.IsActive && i.Quantity <= i.MinimumThreshold)
            .OrderBy(i => i.Quantity)
            .Select(i => new LowStockAlertDto
            {
                ItemId = i.Id,
                ItemName = i.Name,
                Category = i.Category,
                CurrentQuantity = i.Quantity,
                MinimumThreshold = i.MinimumThreshold,
                Supplier = i.Supplier
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryTransactionDto>> GetTransactionsAsync(int hotelId, DateTime? from, DateTime? to)
    {
        var query = _context.InventoryTransactions
            .Include(t => t.InventoryItem)
            .Include(t => t.Room)
            .Include(t => t.CreatedBy)
            .Where(t => t.InventoryItem.HotelId == hotelId);

        if (from.HasValue) query = query.Where(t => t.Date >= from.Value);
        if (to.HasValue) query = query.Where(t => t.Date <= to.Value);

        return await query
            .OrderByDescending(t => t.Date)
            .Select(t => new InventoryTransactionDto
            {
                Id = t.Id,
                InventoryItemId = t.InventoryItemId,
                ItemName = t.InventoryItem.Name,
                RoomId = t.RoomId,
                RoomNumber = t.Room != null ? t.Room.RoomNumber : null,
                Type = t.Type,
                Quantity = t.Quantity,
                Notes = t.Notes,
                Date = t.Date,
                CreatedByUserId = t.CreatedByUserId,
                CreatedByName = t.CreatedBy.FirstName + " " + t.CreatedBy.LastName
            })
            .ToListAsync();
    }

    public async Task<InventoryTransactionDto> RecordTransactionAsync(CreateInventoryTransactionDto dto, string userId)
    {
        var item = await _context.InventoryItems.FindAsync(dto.InventoryItemId)
            ?? throw new KeyNotFoundException($"Inventory item {dto.InventoryItemId} not found");

        var transaction = new InventoryTransaction
        {
            InventoryItemId = dto.InventoryItemId,
            RoomId = dto.RoomId,
            Type = dto.Type,
            Quantity = dto.Quantity,
            Notes = dto.Notes,
            Date = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        // Adjust item quantity
        switch (dto.Type)
        {
            case InventoryTransactionType.Usage:
            case InventoryTransactionType.Damage:
            case InventoryTransactionType.Loss:
                item.Quantity = Math.Max(0, item.Quantity - dto.Quantity);
                break;
            case InventoryTransactionType.Restock:
            case InventoryTransactionType.Return:
                item.Quantity += dto.Quantity;
                item.LastRestocked = DateTime.UtcNow;
                break;
        }

        item.UpdatedAt = DateTime.UtcNow;
        _context.InventoryTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        await _context.Entry(transaction).Reference(t => t.InventoryItem).LoadAsync();
        await _context.Entry(transaction).Reference(t => t.CreatedBy).LoadAsync();
        if (transaction.RoomId.HasValue)
            await _context.Entry(transaction).Reference(t => t.Room).LoadAsync();

        return new InventoryTransactionDto
        {
            Id = transaction.Id,
            InventoryItemId = transaction.InventoryItemId,
            ItemName = transaction.InventoryItem.Name,
            RoomId = transaction.RoomId,
            RoomNumber = transaction.Room?.RoomNumber,
            Type = transaction.Type,
            Quantity = transaction.Quantity,
            Notes = transaction.Notes,
            Date = transaction.Date,
            CreatedByUserId = transaction.CreatedByUserId,
            CreatedByName = transaction.CreatedBy.FirstName + " " + transaction.CreatedBy.LastName
        };
    }

    public async Task<IEnumerable<InventoryCostAnalysisDto>> GetCostAnalysisAsync(int hotelId)
    {
        return await _context.InventoryItems
            .Where(i => i.HotelId == hotelId && i.IsActive)
            .GroupBy(i => i.Category)
            .Select(g => new InventoryCostAnalysisDto
            {
                Category = g.Key,
                ItemCount = g.Count(),
                TotalQuantity = g.Sum(i => i.Quantity),
                TotalValue = g.Sum(i => i.Quantity * i.UnitCost),
                LowStockCount = g.Count(i => i.Quantity <= i.MinimumThreshold)
            })
            .OrderBy(r => r.Category)
            .ToListAsync();
    }

    private static InventoryItemDto MapToDto(InventoryItem i) => new()
    {
        Id = i.Id,
        HotelId = i.HotelId,
        HotelName = i.Hotel?.Name ?? string.Empty,
        Name = i.Name,
        Description = i.Description,
        Category = i.Category,
        Quantity = i.Quantity,
        MinimumThreshold = i.MinimumThreshold,
        UnitCost = i.UnitCost,
        Supplier = i.Supplier,
        Unit = i.Unit,
        LastRestocked = i.LastRestocked,
        IsActive = i.IsActive,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt
    };
}
