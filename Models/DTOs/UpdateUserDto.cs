using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// DTO for updating user profile
/// </summary>
public class UpdateUserDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }
    
    [MaxLength(500)]
    public string? ProfilePictureUrl { get; set; }
    
    // Personal Info
    public DateTime? DateOfBirth { get; set; }
    
    [MaxLength(20)]
    public string? Gender { get; set; }
    
    // Address
    [MaxLength(500)]
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [MaxLength(100)]
    public string? State { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    
    // Professional (for Staff) - Only SuperAdmin can set these
    [MaxLength(200)]
    public string? JobTitle { get; set; }
    
    [MaxLength(200)]
    public string? Department { get; set; }
    
    public int? HotelId { get; set; }
    
    // Emergency Contact
    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }
    
    [MaxLength(50)]
    public string? EmergencyContactPhone { get; set; }
    
    [MaxLength(100)]
    public string? EmergencyContactRelationship { get; set; }
    
    // Preferences
    [MaxLength(50)]
    public string? PreferredLanguage { get; set; }
    
    [MaxLength(100)]
    public string? TimeZone { get; set; }
    
    public bool EmailNotifications { get; set; } = true;
    
    public bool SmsNotifications { get; set; } = false;
    
    // Admin Notes (SuperAdmin only)
    [MaxLength(1000)]
    public string? Notes { get; set; }
}
