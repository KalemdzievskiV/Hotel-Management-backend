using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.DTOs;

public class HotelDto
{
    public int Id { get; set; }
    
    // Ownership (read-only, set from authenticated user)
    public string? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    
    // Basic Information
    [Required(ErrorMessage = "Hotel name is required")]
    [MaxLength(200, ErrorMessage = "Hotel name cannot exceed 200 characters")]
    [MinLength(2, ErrorMessage = "Hotel name must be at least 2 characters")]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
    
    // Location
    [Required(ErrorMessage = "Address is required")]
    [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
    public string Address { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "City is required")]
    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters")]
    public string City { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Country is required")]
    [MaxLength(100, ErrorMessage = "Country cannot exceed 100 characters")]
    public string Country { get; set; } = string.Empty;
    
    [MaxLength(20, ErrorMessage = "Postal code cannot exceed 20 characters")]
    public string? PostalCode { get; set; }
    
    // Contact Information
    [MaxLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
    public string? PhoneNumber { get; set; }
    
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [MaxLength(256, ErrorMessage = "Email cannot exceed 256 characters")]
    public string? Email { get; set; }
    
    [Url(ErrorMessage = "Invalid URL format")]
    [MaxLength(500, ErrorMessage = "Website URL cannot exceed 500 characters")]
    public string? Website { get; set; }
    
    // Rating & Features
    [Range(1, 5, ErrorMessage = "Stars must be between 1 and 5")]
    public int Stars { get; set; } = 3;
    
    [Range(0, 5, ErrorMessage = "Rating must be between 0 and 5")]
    public decimal Rating { get; set; } = 0;
    
    public int TotalReviews { get; set; } = 0;
    
    // Amenities (comma-separated string or array)
    [MaxLength(1000, ErrorMessage = "Amenities cannot exceed 1000 characters")]
    public string? Amenities { get; set; }
    
    // Business Hours
    [MaxLength(100)]
    public string? CheckInTime { get; set; } = "14:00";
    
    [MaxLength(100)]
    public string? CheckOutTime { get; set; } = "11:00";
    
    // Status
    public bool IsActive { get; set; } = true;
    
    // Timestamps (read-only)
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Statistics (read-only, computed)
    public int TotalRooms { get; set; } = 0;
    public int TotalReservations { get; set; } = 0;
}