using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.Entities;

/// <summary>
/// Represents a room in a hotel
/// </summary>
public class Room
{
    public int Id { get; set; }
    
    // Hotel Relationship
    [Required]
    public int HotelId { get; set; }
    
    [ForeignKey("HotelId")]
    public Hotel Hotel { get; set; } = null!;
    
    // Room Identification
    [Required]
    [MaxLength(20)]
    public string RoomNumber { get; set; } = string.Empty;
    
    [Required]
    public RoomType Type { get; set; } = RoomType.Double;
    
    [Range(0, 100)]
    public int Floor { get; set; } = 0;
    
    // Capacity & Pricing
    [Required]
    [Range(1, 20)]
    public int Capacity { get; set; } = 2; // Max number of guests
    
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    [Range(0, 100000)]
    public decimal PricePerNight { get; set; } = 0;
    
    // Short Stay / Hourly Booking Support
    public bool AllowsShortStay { get; set; } = false;
    
    [Column(TypeName = "decimal(10,2)")]
    [Range(0, 10000)]
    public decimal? ShortStayHourlyRate { get; set; }
    
    [Range(1, 24)]
    public int? MinimumShortStayHours { get; set; } = 2;
    
    [Range(1, 24)]
    public int? MaximumShortStayHours { get; set; } = 12;
    
    // Room Details
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(1000)]
    public string? Amenities { get; set; } // e.g., "WiFi,TV,AC,Minibar,Balcony"
    
    [MaxLength(2000)]
    public string? Images { get; set; } // JSON array of image URLs or comma-separated
    
    [Column(TypeName = "decimal(8,2)")]
    [Range(0, 10000)]
    public decimal? AreaSqM { get; set; } // Room area in square meters
    
    // Room Status
    [Required]
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    
    public bool IsActive { get; set; } = true; // Can be booked
    
    // Bedding Configuration
    [MaxLength(200)]
    public string? BedType { get; set; } // e.g., "1 King Bed" or "2 Single Beds"
    
    public bool HasBathtub { get; set; } = false;
    
    public bool HasBalcony { get; set; } = false;
    
    public bool IsSmokingAllowed { get; set; } = false;
    
    // View
    [MaxLength(100)]
    public string? ViewType { get; set; } // e.g., "Sea View", "City View", "Garden View"
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    // Last maintenance/cleaning
    public DateTime? LastCleaned { get; set; }
    
    public DateTime? LastMaintenance { get; set; }
    
    // Notes for staff
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    // Navigation Properties
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
