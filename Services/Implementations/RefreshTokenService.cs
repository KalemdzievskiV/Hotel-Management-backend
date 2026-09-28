using System.Security.Cryptography;
using System.Text;
using HotelManagement.Data;
using HotelManagement.Models;
using HotelManagement.Models.Entities;
using HotelManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelManagement.Services.Implementations;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly TimeProvider _time;

    public RefreshTokenService(ApplicationDbContext context, IOptions<JwtSettings> jwtSettings, TimeProvider time)
    {
        _context = context;
        _jwtSettings = jwtSettings.Value;
        _time = time;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<IssuedRefreshToken> IssueAsync(ApplicationUser user)
    {
        // Expired tokens are of no further use, not even for spotting reuse
        var expired = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresAt <= Now)
            .ToListAsync();
        _context.RefreshTokens.RemoveRange(expired);

        var (token, entity) = NewToken(user);
        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();
        return new IssuedRefreshToken(token, entity.ExpiresAt);
    }

    public async Task<(ApplicationUser User, IssuedRefreshToken Token)?> RotateAsync(string token)
    {
        var existing = await FindAsync(token);
        if (existing == null)
            return null;

        if (existing.RevokedAt != null)
        {
            // A rotated token came back: someone else has a copy, so end every sign-in of this user
            if (existing.ReplacedByTokenId != null)
                await RevokeAllAsync(existing.UserId);
            return null;
        }

        var user = existing.User!;
        if (existing.ExpiresAt <= Now || !user.IsActive || user.SecurityStamp != existing.SecurityStamp)
        {
            existing.RevokedAt = Now;
            await _context.SaveChangesAsync();
            return null;
        }

        var (newToken, replacement) = NewToken(user);
        _context.RefreshTokens.Add(replacement);
        await _context.SaveChangesAsync();

        existing.RevokedAt = Now;
        existing.ReplacedByTokenId = replacement.Id;
        await _context.SaveChangesAsync();

        return (user, new IssuedRefreshToken(newToken, replacement.ExpiresAt));
    }

    public async Task RevokeAsync(string token)
    {
        var existing = await FindAsync(token);
        if (existing == null || existing.RevokedAt != null)
            return;

        existing.RevokedAt = Now;
        await _context.SaveChangesAsync();
    }

    private async Task RevokeAllAsync(string userId)
    {
        var active = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();
        foreach (var t in active)
            t.RevokedAt = Now;
        await _context.SaveChangesAsync();
    }

    private Task<RefreshToken?> FindAsync(string token)
    {
        var hash = Hash(token);
        return _context.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash);
    }

    private (string Token, RefreshToken Entity) NewToken(ApplicationUser user)
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        return (token, new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(token),
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            CreatedAt = Now,
            ExpiresAt = Now.AddDays(_jwtSettings.RefreshTokenDays)
        });
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
