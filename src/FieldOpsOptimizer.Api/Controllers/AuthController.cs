using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Enums;
using System.Security.Claims;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// Authentication and authorization controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns JWT tokens
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Login attempt for user: {UsernameOrEmail}", request.UsernameOrEmail);

            var result = await _authService.AuthenticateAsync(
                request.UsernameOrEmail, 
                request.Password, 
                request.TenantId ?? "default-tenant", 
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Failed login attempt for user: {UsernameOrEmail}", request.UsernameOrEmail);
                return Unauthorized(new ProblemDetails
                {
                    Title = "Authentication failed",
                    Detail = result.ErrorMessage ?? "Invalid credentials",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            _logger.LogInformation("Successful login for user: {Username}", result.User!.Username);

            return Ok(new LoginResponse
            {
                AccessToken = result.AccessToken!,
                RefreshToken = result.RefreshToken!,
                ExpiresAt = result.ExpiresAt!.Value,
                User = new UserInfo
                {
                    Id = result.User.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    FirstName = result.User.FirstName,
                    LastName = result.User.LastName,
                    Roles = result.User.Roles.ToList(),
                    TechnicianId = result.User.TechnicianId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {UsernameOrEmail}", request.UsernameOrEmail);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Registers a new user account
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Registration attempt for user: {Username}", request.Username);

            // Validate password
            if (!_authService.ValidatePassword(request.Password))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid password",
                    Detail = "Password must be at least 8 characters and contain uppercase, lowercase, digit, and special character",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var user = await _authService.CreateUserAsync(
                request.Username,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.TenantId ?? "default-tenant",
                request.Role,
                cancellationToken);

            _logger.LogInformation("Successfully registered user: {Username}", user.Username);

            return CreatedAtAction(nameof(GetProfile), new { }, new RegisterResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = user.Roles.ToList()
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Registration failed for user {Username}: {Error}", request.Username, ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Registration failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed for user {Username}: {Error}", request.Username, ex.Message);
            return BadRequest(new ProblemDetails
            {
                Title = "Registration failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user: {Username}", request.Username);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Refreshes an access token using a valid refresh token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);

            if (!result.Success)
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "Token refresh failed",
                    Detail = result.ErrorMessage ?? "Invalid refresh token",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            return Ok(new LoginResponse
            {
                AccessToken = result.AccessToken!,
                RefreshToken = result.RefreshToken!,
                ExpiresAt = result.ExpiresAt!.Value,
                User = new UserInfo
                {
                    Id = result.User!.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    FirstName = result.User.FirstName,
                    LastName = result.User.LastName,
                    Roles = result.User.Roles.ToList(),
                    TechnicianId = result.User.TechnicianId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Logs out a user by revoking their refresh token
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await _authService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
            _logger.LogInformation("User logged out: {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets the current user's profile information
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult GetProfile()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = User.FindFirstValue(ClaimTypes.Name);
            var email = User.FindFirstValue(ClaimTypes.Email);
            var firstName = User.FindFirstValue(ClaimTypes.GivenName);
            var lastName = User.FindFirstValue(ClaimTypes.Surname);
            var tenantId = User.FindFirstValue("tenant_id");
            var technicianId = User.FindFirstValue("technician_id");
            var roles = User.FindAll(ClaimTypes.Role).Select(c => Enum.Parse<UserRole>(c.Value)).ToList();

            return Ok(new UserProfileResponse
            {
                Id = Guid.Parse(userId!),
                Username = username!,
                Email = email!,
                FirstName = firstName!,
                LastName = lastName!,
                TenantId = tenantId!,
                Roles = roles,
                TechnicianId = technicianId != null ? Guid.Parse(technicianId) : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

// DTOs
public record LoginRequest
{
    public required string UsernameOrEmail { get; init; }
    public required string Password { get; init; }
    public string? TenantId { get; init; }
}

public record RegisterRequest
{
    public required string Username { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? TenantId { get; init; }
    public UserRole Role { get; init; } = UserRole.Technician;
}

public record RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}

public record LogoutRequest
{
    public required string RefreshToken { get; init; }
}

public record LoginResponse
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required UserInfo User { get; init; }
}

public record RegisterResponse
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public List<UserRole> Roles { get; init; } = new();
}

public record UserInfo
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public List<UserRole> Roles { get; init; } = new();
    public Guid? TechnicianId { get; init; }
}

public record UserProfileResponse
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public List<UserRole> Roles { get; init; } = new();
    public Guid? TechnicianId { get; init; }
}
