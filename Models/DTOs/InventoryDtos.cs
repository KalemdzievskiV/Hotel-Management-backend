using System.ComponentModel.DataAnnotations;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

public class InventoryItemDto
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public InventoryCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public int Quantity { get; set; }
    public int MinimumThreshold { get; set; }
    public decimal UnitCost { get; set; }
    public string? Supplier { get; set; }
    public string? Unit { get; set; }
    public DateTime? LastRestocked { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock => Quantity <= MinimumThreshold;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateInventoryItemDto
{
    [Required]
    public int HotelId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public InventoryCategory Category { get; set; }

    [Range(0, 999999)]
    public int Quantity { get; set; } = 0;

    [Range(0, 999999)]
    public int MinimumThreshold { get; set; } = 5;

    [Range(0, 100000)]
    public decimal UnitCost { get; set; } = 0;

    [MaxLength(200)]
    public string? Supplier { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; } = "pcs";
}

public class UpdateInventoryItemDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public InventoryCategory? Category { get; set; }

    [Range(0, 999999)]
    public int? MinimumThreshold { get; set; }

    [Range(0, 100000)]
    public decimal? UnitCost { get; set; }

    [MaxLength(200)]
    public string? Supplier { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    public bool? IsActive { get; set; }
}

public class InventoryTransactionDto
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? RoomId { get; set; }
    public string? RoomNumber { get; set; }
    public InventoryTransactionType Type { get; set; }
    public string TypeName => Type.ToString();
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public DateTime Date { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
}

public class CreateInventoryTransactionDto
{
    [Required]
    public int InventoryItemId { get; set; }

    public int? RoomId { get; set; }

    [Required]
    public InventoryTransactionType Type { get; set; }

    [Required]
    [Range(1, 999999)]
    public int Quantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class InventoryCostAnalysisDto
{
    public InventoryCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public int LowStockCount { get; set; }
}

public class LowStockAlertDto
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public InventoryCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public int CurrentQuantity { get; set; }
    public int MinimumThreshold { get; set; }
    public int ShortageAmount => MinimumThreshold - CurrentQuantity;
    public string? Supplier { get; set; }
}
