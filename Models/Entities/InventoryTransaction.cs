using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

public class InventoryTransaction
{
    public int Id { get; set; }

    [Required]
    public int InventoryItemId { get; set; }

    [ForeignKey("InventoryItemId")]
    public InventoryItem InventoryItem { get; set; } = null!;

    public int? RoomId { get; set; }

    [ForeignKey("RoomId")]
    public Room? Room { get; set; }

    [Required]
    public InventoryTransactionType Type { get; set; }

    [Required]
    [Range(1, 999999)]
    public int Quantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    [ForeignKey("CreatedByUserId")]
    public ApplicationUser CreatedBy { get; set; } = null!;
}
