using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// DTO for SuperAdmin to create new users
/// </summary>
public class CreateUserDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    public string Role { get; set; } = "Guest";
    
    // Optional fields
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    
    // Staff-specific fields (for Admin/Manager/Housekeeper)
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public int? HotelId { get; set; }
    
    // Emergency Contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    
    // Preferences
    public string? PreferredLanguage { get; set; }
    public string? TimeZone { get; set; }
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
    
    // Admin can set user as active/inactive on creation
    public bool IsActive { get; set; } = true;
    
    // Admin notes
    public string? Notes { get; set; }
}
