using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.DTOs;

/// <summary>
/// A hotel owner adding a manager or housekeeper to one of their hotels
/// </summary>
public class CreateStaffDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Manager or Housekeeper</summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    [Required]
    public int HotelId { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [MaxLength(100)]
    public string? JobTitle { get; set; }
}
