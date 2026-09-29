using FluentValidation;
using HotelManagement.Models.DTOs;

namespace HotelManagement.Validators;

public class RegisterDeviceDtoValidator : AbstractValidator<RegisterDeviceDto>
{
    public RegisterDeviceDtoValidator()
    {
        // Only Expo tokens: the server sends through Expo's push service
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Push token is required")
            .MaximumLength(200).WithMessage("Push token is too long")
            .Matches(@"^Expo(nent)?PushToken\[[^\]]+\]$").WithMessage("Not an Expo push token");

        RuleFor(x => x.Platform)
            .Must(p => p is "android" or "ios").WithMessage("Platform must be android or ios");
    }
}
