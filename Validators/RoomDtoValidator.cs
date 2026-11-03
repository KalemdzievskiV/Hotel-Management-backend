using FluentValidation;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.Enums;

namespace HotelManagement.Validators;

public class RoomDtoValidator : AbstractValidator<RoomDto>
{
    public RoomDtoValidator()
    {
        // Hotel ID
        RuleFor(x => x.HotelId)
            .GreaterThan(0).WithMessage("Hotel ID must be greater than 0");
        
        // Room Number
        RuleFor(x => x.RoomNumber)
            .NotEmpty().WithMessage("Room number is required")
            .MinimumLength(1).WithMessage("Room number must be at least 1 character")
            .MaximumLength(20).WithMessage("Room number cannot exceed 20 characters")
            .Matches(@"^[a-zA-Z0-9\-]+$").WithMessage("Room number can only contain letters, numbers, and hyphens");
        
        // Room Type
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid room type");
        
        // Floor
        RuleFor(x => x.Floor)
            .InclusiveBetween(0, 100).WithMessage("Floor must be between 0 and 100");
        
        // Capacity
        RuleFor(x => x.Capacity)
            .InclusiveBetween(1, 20).WithMessage("Capacity must be between 1 and 20 guests");
        
        // Price Per Night
        RuleFor(x => x.PricePerNight)
            .GreaterThanOrEqualTo(0).WithMessage("Price per night cannot be negative")
            .LessThanOrEqualTo(100000).WithMessage("Price per night cannot exceed 100,000");
        
        // Short stay: a fixed length and a flat price, both required when enabled
        RuleFor(x => x.ShortStayHours)
            .NotNull().WithMessage("Short stay length is required when short stay is enabled")
            .InclusiveBetween(1, 23).WithMessage("Short stay length must be between 1 and 23 hours")
            .When(x => x.AllowsShortStay);

        RuleFor(x => x.ShortStayPrice)
            .NotNull().WithMessage("Short stay price is required when short stay is enabled")
            .GreaterThan(0).WithMessage("Short stay price must be greater than 0")
            .LessThanOrEqualTo(100000).WithMessage("Short stay price cannot exceed 100,000")
            .When(x => x.AllowsShortStay);
        
        // Description
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
        
        // Amenities
        RuleFor(x => x.Amenities)
            .MaximumLength(1000).WithMessage("Amenities cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Amenities));
        
        // Images
        RuleFor(x => x.Images)
            .MaximumLength(2000).WithMessage("Images cannot exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Images));
        
        // Area
        RuleFor(x => x.AreaSqM)
            .GreaterThanOrEqualTo(0).WithMessage("Area cannot be negative")
            .LessThanOrEqualTo(10000).WithMessage("Area cannot exceed 10,000 square meters")
            .When(x => x.AreaSqM.HasValue);
        
        // Room Status
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid room status");
        
        // Bed Type
        RuleFor(x => x.BedType)
            .MaximumLength(200).WithMessage("Bed type cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.BedType));
        
        // View Type
        RuleFor(x => x.ViewType)
            .MaximumLength(100).WithMessage("View type cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.ViewType));
        
        // Notes
        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
        
        // Business Rule: Occupied rooms cannot be set to Available
        RuleFor(x => x.Status)
            .NotEqual(RoomStatus.Available)
            .When(x => x.Status == RoomStatus.Occupied)
            .WithMessage("Cannot set an occupied room to available status directly");
    }
}
