using HotelManagement.Models;
using HotelManagement.Models.Constants;
using HotelManagement.Models.DTOs.Auth;
using HotelManagement.Models.Entities;
using HotelManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HotelManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly IEntitlementService _entitlements;
    private readonly IRefreshTokenService _refreshTokens;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings,
        IEntitlementService entitlements,
        IRefreshTokenService refreshTokens)
    {
        _jwtSettings = jwtSettings.Value;
        _entitlements = entitlements;
        _refreshTokens = refreshTokens;
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Public registration only allows Guest role for security
        // SuperAdmin must use /api/Users endpoint to create staff accounts
        if (request.Role != "Guest")
            return BadRequest(new { message = "Public registration only allows Guest role. Contact administrator for staff accounts." });

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return BadRequest(new { message = "User with this email already exists" });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Country = request.Country,
            PostalCode = request.PostalCode,
            IsActive = true,  // New users are active by default
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // Ensure role exists and assign to user
        if (!await _roleManager.RoleExistsAsync(request.Role))
            await _roleManager.CreateAsync(new IdentityRole(request.Role));

        await _userManager.AddToRoleAsync(user, request.Role);

        return Ok(await CreateAuthResponseAsync(user));
    }

    /// <summary>
    /// A hotel owner signs up: they become an Admin with a trial of the top plan and are signed in
    /// </summary>
    [HttpPost("register-owner")]
    public async Task<IActionResult> RegisterOwner([FromBody] RegisterOwnerRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (await _userManager.FindByEmailAsync(request.Email) != null)
            return BadRequest(new { message = "User with this email already exists" });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Country = request.Country,
            JobTitle = "Owner",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, AppRoles.Admin);
        await _entitlements.EnsureSubscriptionAsync(user.Id);

        return Ok(await CreateAuthResponseAsync(user));
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(ApplicationUser user, IssuedRefreshToken? refreshToken = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        refreshToken ??= await _refreshTokens.IssueAsync(user);
        return new AuthResponseDto
        {
            Token = _tokenService.GenerateToken(user, roles),
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt
        };
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token and a new refresh token (the old one stops working)
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        var rotated = await _refreshTokens.RotateAsync(request.RefreshToken);
        if (rotated == null)
            return Unauthorized(new { message = "Your session has ended. Please sign in again." });

        var (user, refreshToken) = rotated.Value;
        return Ok(await CreateAuthResponseAsync(user, refreshToken));
    }

    /// <summary>
    /// Signs this device out: its refresh token stops working
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
    {
        await _refreshTokens.RevokeAsync(request.RefreshToken);
        return NoContent();
    }

    /// <summary>
    /// Changes the signed-in user's password. Every other sign-in ends (the security stamp
    /// changes, so their refresh tokens stop working); this device gets new tokens.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsActive)
            return Unauthorized(new { message = "Your session has ended. Please sign in again." });

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var wrongPassword = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
            return BadRequest(new
            {
                message = wrongPassword
                    ? "Your current password is incorrect"
                    : string.Join(" ", result.Errors.Select(e => e.Description))
            });
        }

        return Ok(await CreateAuthResponseAsync(user));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password" });

        // Check if user is active
        if (!user.IsActive)
            return Unauthorized(new { message = "Your account has been deactivated. Please contact support." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "Too many failed sign-in attempts. Please try again in a few minutes." });
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password" });

        // Update last login date
        user.LastLoginDate = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Ok(await CreateAuthResponseAsync(user));
    }
}
