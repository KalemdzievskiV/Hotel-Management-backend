using FluentAssertions;
using FluentValidation.TestHelper;
using HotelManagement.Models.DTOs;
using HotelManagement.Validators;
using Xunit;

namespace HotelManagement.Tests.Validation;

public class HotelDtoValidatorTests
{
    private readonly HotelDtoValidator _validator;

    public HotelDtoValidatorTests()
    {
        _validator = new HotelDtoValidator();
    }

    #region Name Validation

    [Fact]
    public void Validate_WithValidName_ShouldPass()
    {
        var dto = CreateValidHotelDto();
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Name = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Hotel name is required");
    }

    [Fact]
    public void Validate_WithTooShortName_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Name = "A"; // Only 1 character
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Hotel name must be at least 2 characters");
    }

    [Fact]
    public void Validate_WithTooLongName_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Name = new string('A', 201);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Hotel name cannot exceed 200 characters");
    }

    #endregion

    #region Location Validation

    [Fact]
    public void Validate_WithEmptyAddress_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Address = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Address)
            .WithErrorMessage("Address is required");
    }

    [Fact]
    public void Validate_WithEmptyCity_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.City = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.City)
            .WithErrorMessage("City is required");
    }

    [Fact]
    public void Validate_WithEmptyCountry_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Country = "";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Country)
            .WithErrorMessage("Country is required");
    }

    [Fact]
    public void Validate_WithTooLongAddress_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Address = new string('A', 501);
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Address);
    }

    [Fact]
    public void Validate_WithValidPostalCode_ShouldPass()
    {
        var dto = CreateValidHotelDto();
        dto.PostalCode = "12345";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PostalCode);
    }

    #endregion

    #region Contact Information Validation

    [Fact]
    public void Validate_WithValidEmail_ShouldPass()
    {
        var dto = CreateValidHotelDto();
        dto.Email = "contact@hotel.com";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_WithInvalidEmail_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Email = "not-an-email";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Invalid email format");
    }

    [Fact]
    public void Validate_WithValidPhoneNumber_ShouldPass()
    {
        var dto = CreateValidHotelDto();
        dto.PhoneNumber = "+1-212-555-0100";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public void Validate_WithInvalidPhoneNumber_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.PhoneNumber = "abc-def-ghij";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public void Validate_WithValidWebsite_ShouldPass()
    {
        var dto = CreateValidHotelDto();
        dto.Website = "https://www.hotel.com";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Website);
    }

    [Fact]
    public void Validate_WithInvalidWebsite_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Website = "not-a-url";
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Website)
            .WithErrorMessage("Invalid website URL");
    }

    #endregion

    #region Rating & Features Validation

    [Fact]
    public void Validate_WithStars1To5_ShouldPass()
    {
        for (int stars = 1; stars <= 5; stars++)
        {
            var dto = CreateValidHotelDto();
            dto.Stars = stars;
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.Stars);
        }
    }

    [Fact]
    public void Validate_WithStarsLessThan1_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Stars = 0;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Stars);
    }

    [Fact]
    public void Validate_WithStarsGreaterThan5_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Stars = 6;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Stars);
    }

    [Fact]
    public void Validate_WithValidRating_ShouldPass()
    {
        var dto = CreateValidHotelDto();
        dto.Rating = 4.5m;
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Rating);
    }

    [Fact]
    public void Validate_WithNegativeRating_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Rating = -1;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [Fact]
    public void Validate_WithRatingGreaterThan5_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.Rating = 5.5m;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [Fact]
    public void Validate_WithNegativeTotalReviews_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.TotalReviews = -1;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TotalReviews);
    }

    #endregion

    #region Business Hours Validation

    [Fact]
    public void Validate_WithValidCheckInTime_ShouldPass()
    {
        var dto = CreateValidHotelDto();
        dto.CheckInTime = "14:00";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.CheckInTime);
    }

    [Fact]
    public void Validate_WithInvalidCheckInTime_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.CheckInTime = "25:00"; // Invalid hour
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.CheckInTime);
    }

    [Fact]
    public void Validate_WithValidCheckOutTime_ShouldPass()
    {
        var dto = CreateValidHotelDto();
        dto.CheckOutTime = "11:00";
        var result = _validator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.CheckOutTime);
    }

    [Fact]
    public void Validate_WithInvalidCheckOutTime_ShouldFail()
    {
        var dto = CreateValidHotelDto();
        dto.CheckOutTime = "24:60"; // Invalid minutes
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.CheckOutTime);
    }

    #endregion

    #region Helper Methods

    private HotelDto CreateValidHotelDto()
    {
        return new HotelDto
        {
            Name = "Test Hotel",
            Address = "123 Main St",
            City = "New York",
            Country = "USA",
            Stars = 3,
            Rating = 0,
            TotalReviews = 0
        };
    }

    #endregion
}
