using FluentValidation;
using HotelManagement.Models.DTOs;

namespace HotelManagement.Validators;

public class HotelDtoValidator : AbstractValidator<HotelDto>
{
    public HotelDtoValidator()
    {
        // Basic Information
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Hotel name is required")
            .MinimumLength(2).WithMessage("Hotel name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Hotel name cannot exceed 200 characters");
        
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
        
        // Location
        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required")
            .MaximumLength(500).WithMessage("Address cannot exceed 500 characters");
        
        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters");
        
        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(100).WithMessage("Country cannot exceed 100 characters");
        
        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.PostalCode));
        
        // Contact Information
        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number cannot exceed 20 characters")
            .Matches(@"^[\d\s\-\+\(\)]+$").WithMessage("Phone number can only contain digits, spaces, and symbols: + - ( )")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
        
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters")
            .When(x => !string.IsNullOrEmpty(x.Email));
        
        RuleFor(x => x.Website)
            .MaximumLength(500).WithMessage("Website URL cannot exceed 500 characters")
            .Must(BeAValidUrl).WithMessage("Invalid website URL")
            .When(x => !string.IsNullOrEmpty(x.Website));
        
        // Rating & Features
        RuleFor(x => x.Stars)
            .InclusiveBetween(1, 5).WithMessage("Stars must be between 1 and 5");
        
        RuleFor(x => x.Rating)
            .InclusiveBetween(0, 5).WithMessage("Rating must be between 0 and 5");
        
        RuleFor(x => x.TotalReviews)
            .GreaterThanOrEqualTo(0).WithMessage("Total reviews cannot be negative");
        
        // Amenities
        RuleFor(x => x.Amenities)
            .MaximumLength(1000).WithMessage("Amenities cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Amenities));
        
        // Business Hours
        RuleFor(x => x.CheckInTime)
            .Matches(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]$").WithMessage("Check-in time must be in HH:mm format (e.g., 14:00)")
            .When(x => !string.IsNullOrEmpty(x.CheckInTime));
        
        RuleFor(x => x.CheckOutTime)
            .Matches(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]$").WithMessage("Check-out time must be in HH:mm format (e.g., 11:00)")
            .When(x => !string.IsNullOrEmpty(x.CheckOutTime));
    }
    
    private bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return true;
        
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
