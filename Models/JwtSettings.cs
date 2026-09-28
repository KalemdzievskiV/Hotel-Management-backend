namespace HotelManagement.Models;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
    /// <summary>How long a refresh token (a sign-in on one device) lasts</summary>
    public int RefreshTokenDays { get; set; } = 30;
}
