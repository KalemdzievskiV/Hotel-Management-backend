namespace HotelManagement.Models.DTOs;

/// <summary>
/// What a guest can change about themselves. The email is the sign-in name and stays as it is;
/// VIP, blacklist and notes are the hotel's to set.
/// </summary>
public class UpdateMyProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Nationality { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
}
