using System.IdentityModel.Tokens.Jwt;
using JwtAuthApi_Sonnet45.Application.DTOs;
using JwtAuthApi_Sonnet45.Application.DTOs.Auth;
using JwtAuthApi_Sonnet45.Data;
using JwtAuthApi_Sonnet45.Domain.Entities;
using JwtAuthApi_Sonnet45.Utils;
using UAParser;

namespace JwtAuthApi_Sonnet45.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<TokenResponse> RegisterAsync(RegisterRequest request, string userAgent, string ipAddress)
    {
        // Check if username or email already exists
        var existingUser = await _unitOfWork.Users.GetByUsernameOrEmailAsync(request.Username, request.Email);
        if (existingUser != null)
            throw new InvalidOperationException("Username or email already exists");

        if (!IsDeviceRecognized(userAgent))
        {
            _logger.LogWarning("Unrecognized (device or browser) during registration: {UserAgent}", userAgent);
            throw new InvalidOperationException("Unrecognized device or browser");
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _unitOfWork.Users.AddAsync(user);

        // Assign default "User" role
        var userRole = await _unitOfWork.Roles.GetByNameAsync("User");
        if (userRole != null)
        {
            user.UserRoles.Add(new UserRole
            {
                User = user,
                RoleId = userRole.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("User {Username} registered successfully", user.Username);

        return await GenerateTokenResponseAsync(user, ipAddress);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, string userAgent, string ipAddress)
    {
        var user = await _unitOfWork.Users.GetByUsernameWithRolesAsync(request.Username);

        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
            throw new UnauthorizedAccessException("Invalid username");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid password");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("User account is inactive");

        // Kiem tra user agent có phải từ thiết bị/trình duyệt lạ không
        if (!IsDeviceRecognized(userAgent))
        {
            _logger.LogWarning("Unrecognized (device or browser) during registration: {UserAgent}", userAgent);
            throw new InvalidOperationException("Unrecognized device or browser");
        }

        // Check account lockout
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException($"Account is locked until {user.LockoutEnd.Value}");
        }

        // Reset failed login attempts on successful login
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("User {Username} logged in successfully", user.Username);

        return await GenerateTokenResponseAsync(user, ipAddress);
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        _logger.LogInformation("DEBUG: Received RefreshToken: [{Token}]", refreshToken);

        var token = await _tokenService.GetActiveRefreshTokenAsync(refreshToken);

        // If no match with provided userId, try by refresh token only (client may not know userId when access token expired)
        token ??= await _tokenService.GetActiveRefreshTokenAsync(refreshToken);

        if (token == null)
        {
            _logger.LogWarning("Invalid refresh token attempt"); // Cố gắng refresh token không hợp lệ
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var user = token.User;
        if (user == null)
        {
            _logger.LogWarning("No user associated with refresh token, refreshTokenId {RefreshTokenId}", token.Id);
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        // Revoke old refresh token
        await _tokenService.RevokeRefreshTokenAsync(refreshToken, "Replaced by new token");

        _logger.LogInformation("Old refresh token {RefreshToken} revoked for user {Username}", refreshToken, user.Username);

        // Generate new tokens
        var response = await GenerateTokenResponseAsync(user, ipAddress);

        _logger.LogInformation("Token refreshed for user {Username}", user.Username);

        return response;
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        await _tokenService.RevokeRefreshTokenAsync(refreshToken, "Revoked by user");
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _unitOfWork.Users.GetByUsernameAsync(username);
        return user != null && _passwordHasher.VerifyPassword(password, user.PasswordHash);
    }

    public async Task<TokenResponse> GenerateTokenResponseAsync(User user, string ipAddress)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        JwtSecurityToken accessToken = await _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await _tokenService.CreateRefreshTokenAsync(user, ipAddress);
        await _unitOfWork.RefreshTokens.AddAsync(refreshToken);
        await _unitOfWork.CompleteAsync();

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken),
            RefreshToken = refreshToken.Token ?? Guid.NewGuid().ToString(),
            ExpiresAt = refreshToken.ExpiresAt,
            // User = user,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                Roles = roles
            },
        };
    }

    /// <summary>
    /// Recognized: nhận dạng
    /// </summary>
    /// <param name="userAgent"></param>
    /// <returns></returns>
    private static bool IsDeviceRecognized(string userAgent)
    {
        var parser = Parser.GetDefault();
        ClientInfo clientInfo = parser.Parse(userAgent);

        string uaName = clientInfo.UserAgent?.Family;
        string osName = clientInfo.OS.Family;
        bool isUAAuthenticated = false;
        bool isOSAuthenticated = false;

        foreach (string uan in DevicesAuthenticated.UserAgentNames)
        {
            if (uaName != null && uaName.Equals(uan))
            {
                uaName = uan;
                isUAAuthenticated = true;
                break;
            }
        }

        foreach (string osn in DevicesAuthenticated.OSNames)
        {
            if (osName != null && osName.Equals(osn))
            {
                osName = osn;
                isOSAuthenticated = true;
                break;
            }
        }

        if (uaName != string.Empty && osName != string.Empty)
            GetDeviceAuthenticatedName($"{uaName} - {osName}");

        return isUAAuthenticated && isOSAuthenticated;
    }

    /// <summary>
    /// e.g: Firefox - Windows
    /// </summary>
    /// <param name="nameNomalized">e.g: Firefox - Windows</param>
    /// <returns></returns>
    private static string GetDeviceAuthenticatedName(string nameNomalized)
    {
        return nameNomalized;
    }
}
