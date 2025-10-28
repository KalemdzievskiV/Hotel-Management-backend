using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace HotelManagement.Models.Entities;

/// <summary>
/// Extended ApplicationUser for all user types (Admin, Manager, Guest, Housekeeper)
/// Inherits Email, PhoneNumber, UserName from IdentityUser
/// </summary>
public class ApplicationUser : IdentityUser
{
    // Personal Information
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    // Computed property for backward compatibility
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
    
    [MaxLength(500)]
    public string? ProfilePictureUrl { get; set; }
    
    public DateTime? DateOfBirth { get; set; }
    
    [MaxLength(20)]
    public string? Gender { get; set; }
    
    // Address Information
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
    
    // Professional/Work Information (for Admin/Manager)
    [MaxLength(200)]
    public string? JobTitle { get; set; }
    
    [MaxLength(200)]
    public string? Department { get; set; }
    
    public int? HotelId { get; set; }  // Which hotel they work at (for Admin/Manager/Housekeeper)
    
    [ForeignKey("HotelId")]
    public Hotel? Hotel { get; set; }
    
    // Emergency Contact
    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }
    
    [MaxLength(50)]
    public string? EmergencyContactPhone { get; set; }
    
    [MaxLength(100)]
    public string? EmergencyContactRelationship { get; set; }
    
    // Preferences
    [MaxLength(50)]
    public string? PreferredLanguage { get; set; } = "en";
    
    [MaxLength(100)]
    public string? TimeZone { get; set; }
    
    public bool EmailNotifications { get; set; } = true;
    
    public bool SmsNotifications { get; set; } = false;
    
    // Status & Tracking
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public DateTime? LastLoginDate { get; set; }
    
    // Notes (for internal use by super admin)
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    // Computed properties
    [NotMapped]
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
    
    [NotMapped]
    public bool IsStaff => !string.IsNullOrEmpty(JobTitle) || HotelId.HasValue;
}
