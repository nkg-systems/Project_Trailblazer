using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Application.Common.Interfaces;

/// <summary>
/// Interface for authentication and authorization services
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates a user with username/email and password
    /// </summary>
    Task<AuthResult> AuthenticateAsync(string usernameOrEmail, string password, string tenantId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a new JWT token for the user
    /// </summary>
    Task<string> GenerateJwtTokenAsync(User user, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a refresh token
    /// </summary>
    string GenerateRefreshToken();
    
    /// <summary>
    /// Refreshes the JWT token using a valid refresh token
    /// </summary>
    Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Revokes a refresh token
    /// </summary>
    Task<bool> RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new user account
    /// </summary>
    Task<User> CreateUserAsync(string username, string email, string password, string firstName, string lastName, string tenantId, UserRole role, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a password against security requirements
    /// </summary>
    bool ValidatePassword(string password);
    
    /// <summary>
    /// Hashes a password using BCrypt
    /// </summary>
    string HashPassword(string password);
    
    /// <summary>
    /// Verifies a password against its hash
    /// </summary>
    bool VerifyPassword(string password, string hash);
}

/// <summary>
/// Result of authentication operations
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public User? User { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
