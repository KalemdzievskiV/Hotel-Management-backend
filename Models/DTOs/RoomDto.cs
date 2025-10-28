using System.ComponentModel.DataAnnotations;
using HotelManagement.Models.Enums;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// Data Transfer Object for Room entity
/// </summary>
public class RoomDto
{
    public int Id { get; set; }
    
    // Hotel Relationship
    [Required(ErrorMessage = "Hotel ID is required")]
    public int HotelId { get; set; }
    
    // Read-only: Hotel name for display
    public string? HotelName { get; set; }
    
    // Room Identification
    [Required(ErrorMessage = "Room number is required")]
    [MaxLength(20, ErrorMessage = "Room number cannot exceed 20 characters")]
    [MinLength(1, ErrorMessage = "Room number must be at least 1 character")]
    public string RoomNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Room type is required")]
    public RoomType Type { get; set; } = RoomType.Double;
    
    [Range(0, 100, ErrorMessage = "Floor must be between 0 and 100")]
    public int Floor { get; set; } = 0;
    
    // Capacity & Pricing
    [Required(ErrorMessage = "Capacity is required")]
    [Range(1, 20, ErrorMessage = "Capacity must be between 1 and 20 guests")]
    public int Capacity { get; set; } = 2;
    
    [Required(ErrorMessage = "Price per night is required")]
    [Range(0, 100000, ErrorMessage = "Price must be between 0 and 100,000")]
    public decimal PricePerNight { get; set; } = 0;
    
    // Short Stay / Hourly Booking Support
    public bool AllowsShortStay { get; set; } = false;
    
    [Range(0, 10000, ErrorMessage = "Hourly rate must be between 0 and 10,000")]
    public decimal? ShortStayHourlyRate { get; set; }
    
    [Range(1, 24, ErrorMessage = "Minimum hours must be between 1 and 24")]
    public int? MinimumShortStayHours { get; set; } = 2;
    
    [Range(1, 24, ErrorMessage = "Maximum hours must be between 1 and 24")]
    public int? MaximumShortStayHours { get; set; } = 12;
    
    // Room Details
    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
    
    [MaxLength(1000, ErrorMessage = "Amenities cannot exceed 1000 characters")]
    public string? Amenities { get; set; }
    
    [MaxLength(2000, ErrorMessage = "Images cannot exceed 2000 characters")]
    public string? Images { get; set; }
    
    [Range(0, 10000, ErrorMessage = "Area must be between 0 and 10,000 square meters")]
    public decimal? AreaSqM { get; set; }
    
    // Room Status
    [Required(ErrorMessage = "Room status is required")]
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    
    public bool IsActive { get; set; } = true;
    
    // Bedding Configuration
    [MaxLength(200, ErrorMessage = "Bed type cannot exceed 200 characters")]
    public string? BedType { get; set; }
    
    public bool HasBathtub { get; set; } = false;
    
    public bool HasBalcony { get; set; } = false;
    
    public bool IsSmokingAllowed { get; set; } = false;
    
    // View
    [MaxLength(100, ErrorMessage = "View type cannot exceed 100 characters")]
    public string? ViewType { get; set; }
    
    // Timestamps (Read-only)
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    public DateTime? LastCleaned { get; set; }
    
    public DateTime? LastMaintenance { get; set; }
    
    // Notes
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }
    
    // Computed/Display Properties (Read-only)
    public int TotalReservations { get; set; } = 0;
    
    public bool IsCurrentlyOccupied => Status == RoomStatus.Occupied;
    
    public bool IsAvailableForBooking => IsActive && Status == RoomStatus.Available;
}
