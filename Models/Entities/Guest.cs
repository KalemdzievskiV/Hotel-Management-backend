using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelManagement.Models.Entities;

/// <summary>
/// Represents a guest (walk-in or registered user)
/// Used for reservations and check-ins
/// </summary>
public class Guest
{
    public int Id { get; set; }
    
    // Link to registered user (nullable for walk-in guests)
    // If UserId is set, this is a registered user who signed up
    // If UserId is NULL, this is a walk-in guest created by hotel staff
    public string? UserId { get; set; }
    
    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; }
    
    // Hotel ownership for walk-in guests (NULL for registered users)
    // Walk-in guests belong to a specific hotel
    public int? HotelId { get; set; }
    
    [ForeignKey("HotelId")]
    public Hotel? Hotel { get; set; }
    
    // Track which admin/manager created this walk-in guest
    public string? CreatedByUserId { get; set; }
    
    [ForeignKey("CreatedByUserId")]
    public ApplicationUser? CreatedBy { get; set; }
    
    // Personal Information
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    // Identification
    [MaxLength(100)]
    public string? IdentificationNumber { get; set; } // Passport, ID card, etc.
    
    [MaxLength(50)]
    public string? IdentificationType { get; set; } // Passport, Driver License, National ID
    
    public DateTime? DateOfBirth { get; set; }
    
    [MaxLength(100)]
    public string? Nationality { get; set; }
    
    [MaxLength(10)]
    public string? Gender { get; set; } // Male, Female, Other, Prefer not to say
    
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
    
    // Emergency Contact
    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }
    
    [MaxLength(50)]
    public string? EmergencyContactPhone { get; set; }
    
    [MaxLength(100)]
    public string? EmergencyContactRelationship { get; set; }
    
    // Preferences & Special Requests
    [MaxLength(1000)]
    public string? SpecialRequests { get; set; } // Dietary, accessibility, room preferences
    
    [MaxLength(500)]
    public string? Preferences { get; set; } // Smoking/non-smoking, floor preference, etc.
    
    public bool IsVIP { get; set; } = false;
    
    [MaxLength(100)]
    public string? LoyaltyProgramNumber { get; set; }
    
    // Communication Preferences
    public bool EmailNotifications { get; set; } = true;
    
    public bool SmsNotifications { get; set; } = false;
    
    [MaxLength(50)]
    public string? PreferredLanguage { get; set; } // en, es, fr, de, etc.
    
    // Billing Information
    [MaxLength(200)]
    public string? CompanyName { get; set; } // For business travelers
    
    [MaxLength(100)]
    public string? TaxId { get; set; } // For invoicing
    
    // Notes (for staff use)
    [MaxLength(1000)]
    public string? Notes { get; set; } // Internal notes about guest
    
    // Status
    public bool IsActive { get; set; } = true;
    
    public bool IsBlacklisted { get; set; } = false;
    
    [MaxLength(500)]
    public string? BlacklistReason { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public DateTime? LastStayDate { get; set; } // Last check-out date
    
    // Navigation Properties
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
