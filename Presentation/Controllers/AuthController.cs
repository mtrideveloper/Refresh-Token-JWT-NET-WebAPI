using JwtAuthApi_Sonnet45.Application.DTOs.Auth;
using JwtAuthApi_Sonnet45.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;

namespace JwtAuthApi_Sonnet45.Presentation.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("fixed")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            string userAgent = Request.Headers.UserAgent.ToString();
            string ipAddress = GetIpAddress();

            _logger.LogInformation("User Agent: {UserAgent}", userAgent);

            if (userAgent == string.Empty)
                return BadRequest(new { message = "User agent could not be determined" });
            if (ipAddress == string.Empty)
                return BadRequest(new { message = "IP address could not be determined" });

            var result = await _authService.RegisterAsync(request, userAgent, ipAddress);

            _logger.LogInformation("User {Username} registered {IpAddress}",
                request.Username, ipAddress);

            // what is this code?
            return CreatedAtAction(nameof(GetProfile), new { id = result.User.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error for {Username}", request.Username);
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Login with username and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            string userAgent = Request.Headers.UserAgent.ToString();
            string ipAddress = GetIpAddress();

            if (userAgent == string.Empty)
                return BadRequest(new { message = "User agent could not be determined" });
            if (ipAddress == string.Empty)
                return BadRequest(new { message = "IP address could not be determined" });

            var result = await _authService.LoginAsync(request, userAgent, ipAddress);

            #region COOKIE KHÔNG ĐƯỢC CẬP NHẬT LẠI KHI TẠO TOKEN MỚI
            SetRefreshTokenCookie(result.RefreshToken, result.User.Id);
            #endregion

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for {Username}", request.Username);
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var refreshToken = request?.RefreshToken ?? Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(refreshToken))
                return BadRequest(new { message = "Refresh token is required" });

            var ipAddress = GetIpAddress();

            if (ipAddress == string.Empty)
                return BadRequest(new { message = "IP address could not be determined" });

            var result = await _authService.RefreshTokenAsync(refreshToken, ipAddress);

            SetRefreshTokenCookie(result.RefreshToken, result.User.Id);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh error");
            return StatusCode(500, new { message = "An error occurred during token refresh" });
        }
    }

    /// <summary>
    /// Revoke refresh token (logout)
    /// </summary>
    [Authorize]
    [HttpPost("revoke-token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var refreshToken = request?.RefreshToken ?? Request.Cookies["refreshToken"];
            // var userId = request?.UserId ?? Request.Cookies["userId"];

            if (string.IsNullOrEmpty(refreshToken))
                return BadRequest(new { message = "Refresh token is required" });
            // if (string.IsNullOrEmpty(userId))
            //     return BadRequest(new { message = "User ID is required" });

            var ipAddress = GetIpAddress();

            if (ipAddress == string.Empty)
                return BadRequest(new { message = "IP address could not be determined" });

            await _authService.RevokeTokenAsync(refreshToken);

            Response.Cookies.Delete("refreshToken");

            return Ok(new { message = "Token revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token revocation error");
            return StatusCode(500, new { message = "An error occurred during token revocation" });
        }
    }

    /// <summary>
    /// Logout and revoke all user tokens
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var ipAddress = GetIpAddress();

            // if (deviceId == string.Empty)
            //     return BadRequest(new { message = "Device ID are required in 'X-Device-Id' header" });

            // if (ipAddress == string.Empty)
            //     return BadRequest(new { message = "IP address could not be determined" });

            // temporarily allow null userId for safety
            await _tokenService.RevokeAllUserTokensAsync(userId ?? string.Empty);

            Response.Cookies.Delete("refreshToken");

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout error");
            return StatusCode(500, new { message = "An error occurred during logout" });
        }
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [Authorize]
    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = User.Identity?.Name;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);

            var expClaim = User.FindFirst("exp");
            DateTime? accessTokenExpiresAt = expClaim != null ?
                DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim.Value))
                    .ToOffset(TimeSpan.FromHours(7)) // chuyển sang UTC+7
                    .DateTime
                : null;

            return Ok(new
            {
                id = userId,
                username,
                email,
                roles,
                accessTokenExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Validate if token is still valid
    /// </summary>
    [Authorize]
    [HttpGet("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ValidateToken()
    {
        return Ok(new { valid = true, message = "Token is valid" });
    }

    #region Helper Methods
    /// <summary>
    /// Set refresh token in HttpOnly cookie (each incoming request ?)
    /// </summary>
    private void SetRefreshTokenCookie(string refreshToken, string userId)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(7),
            Secure = true, // HTTPS only
            SameSite = SameSiteMode.Strict,
            IsEssential = true
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        Response.Cookies.Append("userId", userId, cookieOptions);
    }

    private string GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    #endregion
}