using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelManagement.Models.Entities;

public class Hotel
{
    public int Id { get; set; }
    
    // Ownership - Admin who owns this hotel
    [Required]
    public string OwnerId { get; set; } = string.Empty;
    
    [ForeignKey("OwnerId")]
    public ApplicationUser Owner { get; set; } = null!;
    
    // Basic Information
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    // Location
    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    
    // Contact Information
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }
    
    [MaxLength(256)]
    public string? Email { get; set; }
    
    [MaxLength(500)]
    public string? Website { get; set; }
    
    // Rating & Features
    [Range(1, 5)]
    public int Stars { get; set; } = 3; // Hotel star rating (1-5)
    
    [Column(TypeName = "decimal(3,2)")]
    [Range(0, 5)]
    public decimal Rating { get; set; } = 0; // Average customer rating (0-5)
    
    public int TotalReviews { get; set; } = 0;
    
    // Amenities & Features (JSON string or comma-separated)
    [MaxLength(1000)]
    public string? Amenities { get; set; } // e.g., "WiFi,Parking,Pool,Gym,Restaurant"
    
    // Business Hours
    [MaxLength(100)]
    public string? CheckInTime { get; set; } = "14:00"; // Default check-in time
    
    [MaxLength(100)]
    public string? CheckOutTime { get; set; } = "11:00"; // Default check-out time
    
    // Status
    public bool IsActive { get; set; } = true; // Can accept bookings
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation Properties
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}