using HotelManagement.Models.Entities;

namespace HotelManagement.Services.Interfaces;

public interface IRefreshTokenService
{
    /// <summary>A new refresh token for the user; only its hash is stored</summary>
    Task<IssuedRefreshToken> IssueAsync(ApplicationUser user);

    /// <summary>
    /// Exchanges a refresh token for a new one. Null when the token is unknown, expired, revoked,
    /// or the user can no longer sign in. Reusing an already-rotated token revokes all of the user's tokens.
    /// </summary>
    Task<(ApplicationUser User, IssuedRefreshToken Token)?> RotateAsync(string token);

    /// <summary>Ends one sign-in (sign out); unknown tokens are ignored</summary>
    Task RevokeAsync(string token);
}

public record IssuedRefreshToken(string Token, DateTime ExpiresAt);
