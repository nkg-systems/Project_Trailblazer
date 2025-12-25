using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Application.Common.Models;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FieldOpsOptimizer.Infrastructure.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IRepository<User>> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<AuthService>> _mockLogger;
    private readonly JwtSettings _jwtSettings;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mockUserRepository = new Mock<IRepository<User>>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<AuthService>>();
        
        _jwtSettings = new JwtSettings
        {
            Secret = "ThisIsAVerySecureSecretKeyForTesting1234567890",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        };

        var jwtOptions = Options.Create(_jwtSettings);
        _authService = new AuthService(_mockUserRepository.Object, _mockUnitOfWork.Object, jwtOptions, _mockLogger.Object);
    }

    [Fact]
    public void GenerateJwtToken_ShouldReturnValidToken()
    {
        // Arrange
        var user = CreateTestUser("testuser", "test@example.com");

        // Act
        var token = _authService.GenerateJwtToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        
        // Verify token can be parsed and contains expected claims
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Should().NotBeNull();
        // JWT serializes ClaimTypes.NameIdentifier as "nameid" 
        jwtToken.Claims.Should().Contain(c => c.Type == "nameid");
        jwtToken.Claims.Should().Contain(c => c.Type == "email");
    }

    [Fact]
    public void GenerateJwtToken_ShouldIncludeUserClaims()
    {
        // Arrange
        var user = CreateTestUser("testuser", "test@example.com");
        user.AddRole(UserRole.Manager);

        // Act
        var token = _authService.GenerateJwtToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        // Check for claims - JWT doesn't include ClaimTypes.Name in serialization
        // It maps ClaimTypes.Email to "email", ClaimTypes.NameIdentifier to "nameid"
        jwtToken.Claims.Should().Contain(c => c.Type == "email" && c.Value == "test@example.com");
        jwtToken.Claims.Should().Contain(c => c.Type == "role" && c.Value == UserRole.Manager.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == "family_name" && c.Value == "User");
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnUniqueToken()
    {
        // Act
        var token1 = _authService.GenerateRefreshToken();
        var token2 = _authService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBeNullOrEmpty();
        token2.Should().NotBeNullOrEmpty();
        token1.Should().NotBe(token2); // Should be unique
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ShouldReturnSuccess()
    {
        // Arrange
        var password = "TestPassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = CreateTestUserWithPassword("testuser", "test@example.com", passwordHash);

        var users = new List<User> { user };
        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _authService.AuthenticateAsync("testuser", password, "TENANT001");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.User.Should().Be(user);
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidPassword_ShouldReturnFailure()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!");
        var user = CreateTestUserWithPassword("testuser", "test@example.com", passwordHash);

        var users = new List<User> { user };
        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _authService.AuthenticateAsync("testuser", "WrongPassword", "TENANT001");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials");
        result.User.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithNonExistentUser_ShouldReturnFailure()
    {
        // Arrange
        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _authService.AuthenticateAsync("nonexistent", "password", "TENANT001");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task AuthenticateAsync_WithInactiveUser_ShouldReturnFailure()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        var user = CreateTestUserWithPassword("testuser", "test@example.com", passwordHash);
        user.SetActive(false);

        var users = new List<User> { user };
        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        // Act
        var result = await _authService.AuthenticateAsync("testuser", "Password123!", "TENANT001");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmail_ShouldReturnSuccess()
    {
        // Arrange
        var password = "TestPassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = CreateTestUserWithPassword("testuser", "test@example.com", passwordHash);

        var users = new List<User> { user };
        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act - authenticate with email instead of username
        var result = await _authService.AuthenticateAsync("test@example.com", password, "TENANT001");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ShouldReturnNewTokens()
    {
        // Arrange
        var refreshToken = _authService.GenerateRefreshToken();
        var user = CreateTestUser("testuser", "test@example.com");
        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));

        var users = new List<User> { user };
        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _authService.RefreshTokenAsync(refreshToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe(refreshToken); // Should be a new token
    }

    [Fact]
    public async Task RefreshTokenAsync_WithInvalidToken_ShouldReturnFailure()
    {
        // Arrange
        _mockUserRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _authService.RefreshTokenAsync("invalid-token");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid refresh token");
    }

    [Fact]
    public void GenerateJwtToken_WithTechnicianId_ShouldIncludeTechnicianRole()
    {
        // Arrange
        var user = CreateTestUser("testuser", "test@example.com");
        var technicianId = Guid.NewGuid();
        user.LinkToTechnician(technicianId);

        // Act
        var token = _authService.GenerateJwtToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        // Verify user has Technician role when linked
        var allClaims = jwtToken.Claims.ToList();
        var roles = allClaims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        roles.Should().Contain(UserRole.Technician.ToString());
    }

    [Fact]
    public void GenerateJwtToken_WithMultipleRoles_ShouldIncludeAllRoles()
    {
        // Arrange
        var user = CreateTestUser("testuser", "test@example.com");
        user.AddRole(UserRole.Manager);
        user.AddRole(UserRole.Dispatcher);

        // Act
        var token = _authService.GenerateJwtToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        // JWT only includes registered ClaimTypes with short names - "role" is included
        var allClaims = jwtToken.Claims.ToList();
        var roleClaims = allClaims.Where(c => c.Type == "role").ToList();
        roleClaims.Should().HaveCountGreaterThanOrEqualTo(3); // Admin (from constructor) + Manager + Dispatcher
        roleClaims.Select(c => c.Value).Should().Contain(UserRole.Admin.ToString());
        roleClaims.Select(c => c.Value).Should().Contain(UserRole.Manager.ToString());
        roleClaims.Select(c => c.Value).Should().Contain(UserRole.Dispatcher.ToString());
    }

    // Helper methods
    private User CreateTestUser(string username, string email)
    {
        return new User(
            username,
            email,
            "dummy-hash", // Will be replaced in tests that need authentication
            "Test",
            "User",
            "TENANT001",
            UserRole.Admin);
    }

    private User CreateTestUserWithPassword(string username, string email, string passwordHash)
    {
        return new User(
            username,
            email,
            passwordHash,
            "Test",
            "User",
            "TENANT001",
            UserRole.Admin);
    }
}
