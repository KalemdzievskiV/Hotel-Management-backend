namespace HotelManagement.Models.DTOs.Auth;

/// <summary>
/// A hotel owner signing up. They become an Admin with a trial of the top plan,
/// and add their hotels after signing in.
/// </summary>
public class RegisterOwnerRequestDto
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? Country { get; set; }
}
