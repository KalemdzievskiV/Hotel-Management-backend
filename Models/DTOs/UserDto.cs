using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// DTO for ApplicationUser - used for user management
/// </summary>
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    
    // Basic Info
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    public string FullName => $"{FirstName} {LastName}";
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    public string? PhoneNumber { get; set; }
    
    public string? ProfilePictureUrl { get; set; }
    
    // Personal Info
    public DateTime? DateOfBirth { get; set; }
    
    public string? Gender { get; set; }
    
    public int? Age
    {
        get
        {
            if (!DateOfBirth.HasValue)
                return null;
            
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Value.Year;
            if (DateOfBirth.Value.Date > today.AddYears(-age))
                age--;
            
            return age;
        }
    }
    
    // Address
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    
    // Professional (for Staff)
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public int? HotelId { get; set; }
    public string? HotelName { get; set; }
    public bool IsStaff => !string.IsNullOrEmpty(JobTitle) || HotelId.HasValue;
    
    // Emergency Contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    
    // Preferences
    public string? PreferredLanguage { get; set; }
    public string? TimeZone { get; set; }
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
    
    // Status
    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginDate { get; set; }
    
    // Roles
    public List<string> Roles { get; set; } = new();
    
    // Admin Notes
    public string? Notes { get; set; }
}
