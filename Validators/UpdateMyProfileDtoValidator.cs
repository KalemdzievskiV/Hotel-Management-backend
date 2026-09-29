using FluentValidation;
using HotelManagement.Models.DTOs;

namespace HotelManagement.Validators;

public class UpdateMyProfileDtoValidator : AbstractValidator<UpdateMyProfileDto>
{
    public UpdateMyProfileDtoValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MinimumLength(2).WithMessage("First name must be at least 2 characters")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters")
            .Matches(RegisterRequestDtoValidator.NamePattern).WithMessage("First name can only contain letters, spaces, hyphens, apostrophes, and periods");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MinimumLength(2).WithMessage("Last name must be at least 2 characters")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters")
            .Matches(RegisterRequestDtoValidator.NamePattern).WithMessage("Last name can only contain letters, spaces, hyphens, apostrophes, and periods");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50).WithMessage("Phone number cannot exceed 50 characters")
            .Matches(@"^[\d\s\-\+\(\)\.]+$").WithMessage("Phone number contains invalid characters")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.Today).WithMessage("Date of birth must be in the past")
            .GreaterThan(DateTime.Today.AddYears(-150)).WithMessage("Date of birth is too far in the past")
            .When(x => x.DateOfBirth.HasValue);

        RuleFor(x => x.Nationality).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.PostalCode).MaximumLength(20);
    }
}
