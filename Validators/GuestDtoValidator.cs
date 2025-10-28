using FluentValidation;
using HotelManagement.Models.DTOs;

namespace HotelManagement.Validators;

public class GuestDtoValidator : AbstractValidator<GuestDto>
{
    public GuestDtoValidator()
    {
        // Personal Information - Names
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MinimumLength(2).WithMessage("First name must be at least 2 characters")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters")
            .Matches(@"^[a-zA-Z\s\-'\.]+$").WithMessage("First name can only contain letters, spaces, hyphens, apostrophes, and periods");
        
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MinimumLength(2).WithMessage("Last name must be at least 2 characters")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters")
            .Matches(@"^[a-zA-Z\s\-'\.]+$").WithMessage("Last name can only contain letters, spaces, hyphens, apostrophes, and periods");
        
        // Contact Information
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(200).WithMessage("Email cannot exceed 200 characters");
        
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .MaximumLength(50).WithMessage("Phone number cannot exceed 50 characters")
            .Matches(@"^[\d\s\-\+\(\)\.]+$").WithMessage("Phone number contains invalid characters");
        
        // Identification
        RuleFor(x => x.IdentificationNumber)
            .MaximumLength(100).WithMessage("Identification number cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.IdentificationNumber));
        
        RuleFor(x => x.IdentificationType)
            .MaximumLength(50).WithMessage("Identification type cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.IdentificationType));
        
        // Date of Birth validation
        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.Today).WithMessage("Date of birth must be in the past")
            .GreaterThan(DateTime.Today.AddYears(-150)).WithMessage("Date of birth is too far in the past")
            .When(x => x.DateOfBirth.HasValue);
        
        // Age validation (must be at least 18 for primary guest)
        RuleFor(x => x.DateOfBirth)
            .Must(BeAtLeast18YearsOld).WithMessage("Guest must be at least 18 years old")
            .When(x => x.DateOfBirth.HasValue);
        
        RuleFor(x => x.Nationality)
            .MaximumLength(100).WithMessage("Nationality cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Nationality));
        
        RuleFor(x => x.Gender)
            .MaximumLength(10).WithMessage("Gender cannot exceed 10 characters")
            .When(x => !string.IsNullOrEmpty(x.Gender));
        
        // Address Information
        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Address));
        
        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.City));
        
        RuleFor(x => x.State)
            .MaximumLength(100).WithMessage("State cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.State));
        
        RuleFor(x => x.Country)
            .MaximumLength(100).WithMessage("Country cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Country));
        
        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.PostalCode));
        
        // Emergency Contact
        RuleFor(x => x.EmergencyContactName)
            .MaximumLength(200).WithMessage("Emergency contact name cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.EmergencyContactName));
        
        RuleFor(x => x.EmergencyContactPhone)
            .MaximumLength(50).WithMessage("Emergency contact phone cannot exceed 50 characters")
            .Matches(@"^[\d\s\-\+\(\)\.]+$").WithMessage("Emergency contact phone contains invalid characters")
            .When(x => !string.IsNullOrEmpty(x.EmergencyContactPhone));
        
        RuleFor(x => x.EmergencyContactRelationship)
            .MaximumLength(100).WithMessage("Emergency contact relationship cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.EmergencyContactRelationship));
        
        // Preferences & Special Requests
        RuleFor(x => x.SpecialRequests)
            .MaximumLength(1000).WithMessage("Special requests cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.SpecialRequests));
        
        RuleFor(x => x.Preferences)
            .MaximumLength(500).WithMessage("Preferences cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Preferences));
        
        RuleFor(x => x.LoyaltyProgramNumber)
            .MaximumLength(100).WithMessage("Loyalty program number cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.LoyaltyProgramNumber));
        
        RuleFor(x => x.PreferredLanguage)
            .MaximumLength(50).WithMessage("Preferred language cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.PreferredLanguage));
        
        // Billing Information
        RuleFor(x => x.CompanyName)
            .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.CompanyName));
        
        RuleFor(x => x.TaxId)
            .MaximumLength(100).WithMessage("Tax ID cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.TaxId));
        
        // Notes
        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
        
        // Blacklist validation
        RuleFor(x => x.BlacklistReason)
            .NotEmpty().WithMessage("Blacklist reason is required when guest is blacklisted")
            .MaximumLength(500).WithMessage("Blacklist reason cannot exceed 500 characters")
            .When(x => x.IsBlacklisted);
        
        RuleFor(x => x.BlacklistReason)
            .Empty().WithMessage("Blacklist reason should be empty when guest is not blacklisted")
            .When(x => !x.IsBlacklisted);
    }
    
    private bool BeAtLeast18YearsOld(DateTime? dateOfBirth)
    {
        if (!dateOfBirth.HasValue)
            return true; // If no DOB provided, skip this validation
        
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Value.Year;
        if (dateOfBirth.Value.Date > today.AddYears(-age))
            age--;
        
        return age >= 18;
    }
}
