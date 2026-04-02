using HotelManagement.Models.DTOs;

namespace HotelManagement.Services.Interfaces;

public interface IInventoryService
{
    Task<IEnumerable<InventoryItemDto>> GetItemsByHotelAsync(int hotelId, bool includeInactive = false);
    Task<InventoryItemDto?> GetItemByIdAsync(int id);
    Task<InventoryItemDto> CreateItemAsync(CreateInventoryItemDto dto, string userId);
    Task<InventoryItemDto> UpdateItemAsync(int id, UpdateInventoryItemDto dto);
    Task DeleteItemAsync(int id);
    Task<IEnumerable<LowStockAlertDto>> GetLowStockItemsAsync(int hotelId);
    Task<IEnumerable<InventoryTransactionDto>> GetTransactionsAsync(int hotelId, DateTime? from, DateTime? to);
    Task<InventoryTransactionDto> RecordTransactionAsync(CreateInventoryTransactionDto dto, string userId);
    Task<IEnumerable<InventoryCostAnalysisDto>> GetCostAnalysisAsync(int hotelId);
}
