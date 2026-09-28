using System.ComponentModel.DataAnnotations;

namespace HotelManagement.Models.Entities;

/// <summary>
/// A long-lived sign-in that can be exchanged for a new access token. Only a hash of the token
/// is stored. Each exchange replaces the token with a new one (rotation); presenting a token
/// that was already replaced means it leaked, so all of the user's sign-ins are revoked.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    /// <summary>SHA-256 of the token, hex encoded</summary>
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// The user's security stamp when the token was issued; a later change (deactivation, role
    /// or password change) makes the token unusable, like it does for access tokens
    /// </summary>
    [MaxLength(256)]
    public string SecurityStamp { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Set when the token was rotated, rather than revoked by sign-out</summary>
    public int? ReplacedByTokenId { get; set; }
}
