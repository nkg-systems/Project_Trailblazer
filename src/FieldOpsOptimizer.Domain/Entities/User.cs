using FieldOpsOptimizer.Domain.Common;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Domain.Entities;

/// <summary>
/// Represents a user in the system with authentication and authorization capabilities
/// </summary>
public class User : BaseEntity
{
    private readonly List<UserRole> _roles = new();

    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public bool IsEmailVerified { get; private set; } = false;
    public DateTime? LastLoginAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiryTime { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    
    /// <summary>
    /// Reference to the technician if this user is a technician
    /// </summary>
    public Guid? TechnicianId { get; private set; }
    
    public IReadOnlyList<UserRole> Roles => _roles.AsReadOnly();
    public string FullName => $"{FirstName} {LastName}".Trim();

    // Navigation properties
    public Technician? Technician { get; private set; }

    private User() { } // For EF Core

    public User(
        string username,
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string tenantId,
        UserRole primaryRole)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty", nameof(username));
        
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));
        
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty", nameof(passwordHash));
        
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));

        Username = username.ToLowerInvariant();
        Email = email.ToLowerInvariant();
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        TenantId = tenantId;
        
        _roles.Add(primaryRole);
    }

    public void UpdateProfile(string firstName, string lastName, string email)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email.ToLowerInvariant();
        UpdateTimestamp();
    }

    public void UpdatePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty", nameof(passwordHash));
        
        PasswordHash = passwordHash;
        UpdateTimestamp();
    }

    public void AddRole(UserRole role)
    {
        if (!_roles.Contains(role))
        {
            _roles.Add(role);
            UpdateTimestamp();
        }
    }

    public void RemoveRole(UserRole role)
    {
        if (_roles.Count > 1 && _roles.Contains(role))
        {
            _roles.Remove(role);
            UpdateTimestamp();
        }
    }

    public bool HasRole(UserRole role)
    {
        return _roles.Contains(role);
    }

    public bool HasAnyRole(params UserRole[] roles)
    {
        return roles.Any(role => _roles.Contains(role));
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdateTimestamp();
    }

    public void SetEmailVerified(bool isVerified)
    {
        IsEmailVerified = isVerified;
        UpdateTimestamp();
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void SetRefreshToken(string refreshToken, DateTime expiryTime)
    {
        RefreshToken = refreshToken;
        RefreshTokenExpiryTime = expiryTime;
        UpdateTimestamp();
    }

    public void ClearRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiryTime = null;
        UpdateTimestamp();
    }

    public void LinkToTechnician(Guid technicianId)
    {
        TechnicianId = technicianId;
        if (!HasRole(UserRole.Technician))
        {
            AddRole(UserRole.Technician);
        }
        UpdateTimestamp();
    }

    public void UnlinkFromTechnician()
    {
        TechnicianId = null;
        RemoveRole(UserRole.Technician);
        UpdateTimestamp();
    }

    public bool IsRefreshTokenValid(string refreshToken)
    {
        return RefreshToken == refreshToken && 
               RefreshTokenExpiryTime.HasValue && 
               RefreshTokenExpiryTime.Value > DateTime.UtcNow;
    }
}
