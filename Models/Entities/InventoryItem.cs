using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

public class InventoryItem
{
    public int Id { get; set; }

    [Required]
    public int HotelId { get; set; }

    [ForeignKey("HotelId")]
    public Hotel Hotel { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public InventoryCategory Category { get; set; }

    [Required]
    [Range(0, 999999)]
    public int Quantity { get; set; } = 0;

    [Required]
    [Range(0, 999999)]
    public int MinimumThreshold { get; set; } = 5;

    [Column(TypeName = "decimal(10,2)")]
    [Range(0, 100000)]
    public decimal UnitCost { get; set; } = 0;

    [MaxLength(200)]
    public string? Supplier { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; } = "pcs";

    public DateTime? LastRestocked { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();

    [NotMapped]
    public bool IsLowStock => Quantity <= MinimumThreshold;
}
