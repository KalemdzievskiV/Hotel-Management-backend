using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// Data Transfer Object for Guest entity
/// </summary>
public class GuestDto
{
    public int Id { get; set; }
    
    // Link to registered user (nullable for walk-in guests)
    public string? UserId { get; set; }
    
    // Display property
    public string? UserName { get; set; }
    
    // Hotel ownership for walk-in guests (NULL for registered users)
    public int? HotelId { get; set; }
    
    // Display property
    public string? HotelName { get; set; }
    
    // Track who created this walk-in guest
    public string? CreatedByUserId { get; set; }
    
    // Display property
    public string? CreatedByUserName { get; set; }
    
    // Personal Information
    [Required(ErrorMessage = "First name is required")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    [MinLength(2, ErrorMessage = "First name must be at least 2 characters")]
    public string FirstName { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Last name is required")]
    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    [MinLength(2, ErrorMessage = "Last name must be at least 2 characters")]
    public string LastName { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Email is required")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Phone number is required")]
    [MaxLength(50, ErrorMessage = "Phone number cannot exceed 50 characters")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    // Identification
    [MaxLength(100, ErrorMessage = "Identification number cannot exceed 100 characters")]
    public string? IdentificationNumber { get; set; }
    
    [MaxLength(50, ErrorMessage = "Identification type cannot exceed 50 characters")]
    public string? IdentificationType { get; set; }
    
    public DateTime? DateOfBirth { get; set; }
    
    [MaxLength(100, ErrorMessage = "Nationality cannot exceed 100 characters")]
    public string? Nationality { get; set; }
    
    [MaxLength(10, ErrorMessage = "Gender cannot exceed 10 characters")]
    public string? Gender { get; set; }
    
    // Address Information
    [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
    public string? Address { get; set; }
    
    [MaxLength(100, ErrorMessage = "City cannot exceed 100 characters")]
    public string? City { get; set; }
    
    [MaxLength(100, ErrorMessage = "State cannot exceed 100 characters")]
    public string? State { get; set; }
    
    [MaxLength(100, ErrorMessage = "Country cannot exceed 100 characters")]
    public string? Country { get; set; }
    
    [MaxLength(20, ErrorMessage = "Postal code cannot exceed 20 characters")]
    public string? PostalCode { get; set; }
    
    // Emergency Contact
    [MaxLength(200, ErrorMessage = "Emergency contact name cannot exceed 200 characters")]
    public string? EmergencyContactName { get; set; }
    
    [MaxLength(50, ErrorMessage = "Emergency contact phone cannot exceed 50 characters")]
    public string? EmergencyContactPhone { get; set; }
    
    [MaxLength(100, ErrorMessage = "Emergency contact relationship cannot exceed 100 characters")]
    public string? EmergencyContactRelationship { get; set; }
    
    // Preferences & Special Requests
    [MaxLength(1000, ErrorMessage = "Special requests cannot exceed 1000 characters")]
    public string? SpecialRequests { get; set; }
    
    [MaxLength(500, ErrorMessage = "Preferences cannot exceed 500 characters")]
    public string? Preferences { get; set; }
    
    public bool IsVIP { get; set; } = false;
    
    [MaxLength(100, ErrorMessage = "Loyalty program number cannot exceed 100 characters")]
    public string? LoyaltyProgramNumber { get; set; }
    
    // Communication Preferences
    public bool EmailNotifications { get; set; } = true;
    
    public bool SmsNotifications { get; set; } = false;
    
    [MaxLength(50, ErrorMessage = "Preferred language cannot exceed 50 characters")]
    public string? PreferredLanguage { get; set; }
    
    // Billing Information
    [MaxLength(200, ErrorMessage = "Company name cannot exceed 200 characters")]
    public string? CompanyName { get; set; }
    
    [MaxLength(100, ErrorMessage = "Tax ID cannot exceed 100 characters")]
    public string? TaxId { get; set; }
    
    // Notes (for staff use)
    [MaxLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }
    
    // Status
    public bool IsActive { get; set; } = true;
    
    public bool IsBlacklisted { get; set; } = false;
    
    [MaxLength(500, ErrorMessage = "Blacklist reason cannot exceed 500 characters")]
    public string? BlacklistReason { get; set; }
    
    // Timestamps (Read-only)
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    public DateTime? LastStayDate { get; set; }
    
    // Computed properties (Read-only)
    public int TotalReservations { get; set; } = 0;
    
    public string FullName => $"{FirstName} {LastName}";
    
    // True if this is a registered user (signed up), False if walk-in guest
    public bool IsRegisteredUser => !string.IsNullOrEmpty(UserId);
    
    // True if this is a walk-in guest created by hotel staff
    public bool IsWalkInGuest => !string.IsNullOrEmpty(CreatedByUserId) && HotelId.HasValue;
    
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
}
