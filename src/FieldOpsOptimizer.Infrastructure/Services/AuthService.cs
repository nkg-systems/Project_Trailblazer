using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BCrypt.Net;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Application.Common.Models;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FieldOpsOptimizer.Infrastructure.Services;

/// <summary>
/// Implementation of authentication and authorization services
/// </summary>
public class AuthService : IAuthService
{
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IRepository<User> userRepository,
        IUnitOfWork unitOfWork,
        IOptions<JwtSettings> jwtSettings)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResult> AuthenticateAsync(string usernameOrEmail, string password, string tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find user by username or email within the tenant
            var user = (await _userRepository.GetAllAsync(cancellationToken))
                .FirstOrDefault(u => 
                    (u.Username == usernameOrEmail.ToLowerInvariant() || u.Email == usernameOrEmail.ToLowerInvariant()) &&
                    u.TenantId == tenantId &&
                    u.IsActive);

            if (user == null)
            {
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = "Invalid credentials"
                };
            }

            // Verify password
            if (!VerifyPassword(password, user.PasswordHash))
            {
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = "Invalid credentials"
                };
            }

            // Update last login
            user.UpdateLastLogin();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Generate tokens
            var accessToken = await GenerateJwtTokenAsync(user, cancellationToken);
            var refreshToken = GenerateRefreshToken();
            var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

            // Store refresh token
            user.SetRefreshToken(refreshToken, refreshTokenExpiry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new AuthResult
            {
                Success = true,
                User = user,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes)
            };
        }
        catch (Exception ex)
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Authentication failed"
            };
        }
    }

    public async Task<string> GenerateJwtTokenAsync(User user, CancellationToken cancellationToken = default)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSettings.Secret);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("tenant_id", user.TenantId)
        };

        // Add role claims
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }

        // Add technician ID if linked
        if (user.TechnicianId.HasValue)
        {
            claims.Add(new Claim("technician_id", user.TechnicianId.Value.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = (await _userRepository.GetAllAsync(cancellationToken))
                .FirstOrDefault(u => u.RefreshToken == refreshToken && u.IsActive);

            if (user == null || !user.IsRefreshTokenValid(refreshToken))
            {
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = "Invalid refresh token"
                };
            }

            // Generate new tokens
            var newAccessToken = await GenerateJwtTokenAsync(user, cancellationToken);
            var newRefreshToken = GenerateRefreshToken();
            var newRefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

            // Update refresh token
            user.SetRefreshToken(newRefreshToken, newRefreshTokenExpiry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new AuthResult
            {
                Success = true,
                User = user,
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes)
            };
        }
        catch (Exception ex)
        {
            return new AuthResult
            {
                Success = false,
                ErrorMessage = "Token refresh failed"
            };
        }
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = (await _userRepository.GetAllAsync(cancellationToken))
                .FirstOrDefault(u => u.RefreshToken == refreshToken);

            if (user == null)
                return false;

            user.ClearRefreshToken();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<User> CreateUserAsync(string username, string email, string password, string firstName, string lastName, string tenantId, UserRole role, CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (!ValidatePassword(password))
            throw new ArgumentException("Password does not meet security requirements");

        // Check if username or email already exists in tenant
        var existingUser = (await _userRepository.GetAllAsync(cancellationToken))
            .Any(u => 
                (u.Username == username.ToLowerInvariant() || u.Email == email.ToLowerInvariant()) &&
                u.TenantId == tenantId);

        if (existingUser)
            throw new InvalidOperationException("Username or email already exists");

        // Hash password and create user
        var passwordHash = HashPassword(password);
        var user = new User(username, email, passwordHash, firstName, lastName, tenantId, role);

        _userRepository.Add(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user;
    }

    public bool ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        // Password requirements:
        // - At least 8 characters
        // - At least one uppercase letter
        // - At least one lowercase letter
        // - At least one digit
        // - At least one special character
        return password.Length >= 8 &&
               Regex.IsMatch(password, @"[A-Z]") &&
               Regex.IsMatch(password, @"[a-z]") &&
               Regex.IsMatch(password, @"\d") &&
               Regex.IsMatch(password, @"[^a-zA-Z\d\s]");
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}
