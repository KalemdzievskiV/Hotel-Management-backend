using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HotelManagement.Models.DTOs;
using HotelManagement.Models.DTOs.Auth;
using Xunit;

namespace HotelManagement.Tests.Validation;

public class DtoValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    #region HotelDto Tests

    [Fact]
    public void HotelDto_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var hotelDto = new HotelDto
        {
            Name = "Valid Hotel Name",
            Address = "123 Main Street",
            City = "New York",
            Country = "USA"
        };

        // Act
        var validationResults = ValidateModel(hotelDto);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void HotelDto_WithEmptyName_ShouldFailValidation()
    {
        // Arrange
        var hotelDto = new HotelDto
        {
            Name = ""
        };

        // Act
        var validationResults = ValidateModel(hotelDto);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.ErrorMessage!.Contains("required"));
    }

    [Fact]
    public void HotelDto_WithTooShortName_ShouldFailValidation()
    {
        // Arrange
        var hotelDto = new HotelDto
        {
            Name = "A" // Only 1 character, min is 2
        };

        // Act
        var validationResults = ValidateModel(hotelDto);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.ErrorMessage!.Contains("at least 2 characters"));
    }

    [Fact]
    public void HotelDto_WithTooLongName_ShouldFailValidation()
    {
        // Arrange
        var hotelDto = new HotelDto
        {
            Name = new string('A', 201) // 201 characters, max is 200
        };

        // Act
        var validationResults = ValidateModel(hotelDto);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.ErrorMessage!.Contains("cannot exceed 200 characters"));
    }

    #endregion

    #region RegisterRequestDto Tests

    [Fact]
    public void RegisterRequestDto_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var registerDto = new RegisterRequestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Password = "SecurePassword123",
            Role = "Guest"
        };

        // Act
        var validationResults = ValidateModel(registerDto);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void RegisterRequestDto_WithInvalidEmail_ShouldFailValidation()
    {
        // Arrange
        var registerDto = new RegisterRequestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "invalid-email",
            Password = "SecurePassword123",
            Role = "Guest"
        };

        // Act
        var validationResults = ValidateModel(registerDto);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.MemberNames.Contains("Email"));
    }

    [Fact]
    public void RegisterRequestDto_WithShortPassword_ShouldFailValidation()
    {
        // Arrange
        var registerDto = new RegisterRequestDto
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Password = "12345", // Only 5 characters
            Role = "Guest"
        };

        // Act
        var validationResults = ValidateModel(registerDto);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.ErrorMessage!.Contains("at least 6 characters"));
    }

    [Fact]
    public void RegisterRequestDto_WithTooLongFullName_ShouldFailValidation()
    {
        // Arrange
        var registerDto = new RegisterRequestDto
        {
            FirstName = new string('A', 101), // 101 characters, max is 100
            LastName = "Doe",
            Email = "john@example.com",
            Password = "SecurePassword123",
            Role = "Guest"
        };

        // Act
        var validationResults = ValidateModel(registerDto);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.ErrorMessage!.Contains("cannot exceed 100 characters"));
    }

    [Fact]
    public void RegisterRequestDto_WithEmptyFullName_ShouldFailValidation()
    {
        // Arrange
        var registerDto = new RegisterRequestDto
        {
            FirstName = "",
            LastName = "Doe",
            Email = "john@example.com",
            Password = "SecurePassword123",
            Role = "Guest"
        };

        // Act
        var validationResults = ValidateModel(registerDto);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.ErrorMessage!.Contains("Full name is required"));
    }

    #endregion

    #region LoginRequestDto Tests

    [Fact]
    public void LoginRequestDto_WithValidData_ShouldPassValidation()
    {
        // Arrange
        var loginDto = new LoginRequestDto
        {
            Email = "user@example.com",
            Password = "Password123"
        };

        // Act
        var validationResults = ValidateModel(loginDto);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void LoginRequestDto_WithMissingEmail_ShouldFailValidation()
    {
        // Arrange
        var loginDto = new LoginRequestDto
        {
            Email = "",
            Password = "Password123"
        };

        // Act
        var validationResults = ValidateModel(loginDto);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.MemberNames.Contains("Email"));
    }

    [Fact]
    public void LoginRequestDto_WithMissingPassword_ShouldFailValidation()
    {
        // Arrange
        var loginDto = new LoginRequestDto
        {
            Email = "user@example.com",
            Password = ""
        };

        // Act
        var validationResults = ValidateModel(loginDto);

        // Assert
        validationResults.Should().NotBeEmpty();
        validationResults.Should().Contain(v => v.MemberNames.Contains("Password"));
    }

    #endregion
}
