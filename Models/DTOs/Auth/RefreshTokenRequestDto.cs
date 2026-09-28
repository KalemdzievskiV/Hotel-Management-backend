using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.DTOs.Auth;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
