using FieldOpsOptimizer.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace FieldOpsOptimizer.Application.Tests.Services;

public class TenantServiceTests
{
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ILogger<TenantService>> _mockLogger;
    private readonly TenantService _tenantService;

    public TenantServiceTests()
    {
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<TenantService>>();
        _tenantService = new TenantService(_mockHttpContextAccessor.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetCurrentTenantId_ShouldReturnTenantId_WhenClaimExists()
    {
        // Arrange
        var expectedTenantId = "tenant-123";
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@example.com"),
            new("tenant_id", expectedTenantId)
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentTenantId();

        // Assert
        result.Should().Be(expectedTenantId);
    }

    [Fact]
    public void GetCurrentTenantId_ShouldReturnDefaultTenant_WhenNoClaimExists()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@example.com")
            // No tenant_id claim
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentTenantId();

        // Assert
        result.Should().Be("default");
    }

    [Fact]
    public void GetCurrentTenantId_ShouldReturnDefaultTenant_WhenHttpContextIsNull()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _tenantService.GetCurrentTenantId();

        // Assert
        result.Should().Be("default");
    }

    [Fact]
    public void GetCurrentTenantId_ShouldReturnDefaultTenant_WhenUserIsNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            User = null
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentTenantId();

        // Assert
        result.Should().Be("default");
    }

    [Fact]
    public void GetCurrentUserId_ShouldReturnUserId_WhenNameIdentifierClaimExists()
    {
        // Arrange
        var expectedUserId = "user-456";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, expectedUserId),
            new(ClaimTypes.Name, "test@example.com")
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentUserId();

        // Assert
        result.Should().Be(expectedUserId);
    }

    [Fact]
    public void GetCurrentUserId_ShouldReturnNull_WhenNoNameIdentifierClaimExists()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@example.com"),
            new("tenant_id", "tenant-123")
            // No NameIdentifier claim
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentUserId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_ShouldReturnNull_WhenHttpContextIsNull()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _tenantService.GetCurrentUserId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserName_ShouldReturnUserName_WhenNameClaimExists()
    {
        // Arrange
        var expectedUserName = "test@example.com";
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, expectedUserName),
            new(ClaimTypes.NameIdentifier, "user-123")
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentUserName();

        // Assert
        result.Should().Be(expectedUserName);
    }

    [Fact]
    public void GetCurrentUserName_ShouldReturnNull_WhenNoNameClaimExists()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-123"),
            new("tenant_id", "tenant-123")
            // No Name claim
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentUserName();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void IsCurrentUserInTenant_ShouldReturnTrue_WhenUserBelongsToTenant()
    {
        // Arrange
        var tenantId = "tenant-789";
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@example.com"),
            new("tenant_id", tenantId)
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.IsCurrentUserInTenant(tenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCurrentUserInTenant_ShouldReturnFalse_WhenUserBelongsToDifferentTenant()
    {
        // Arrange
        var actualTenantId = "tenant-789";
        var testTenantId = "tenant-999";
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@example.com"),
            new("tenant_id", actualTenantId)
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.IsCurrentUserInTenant(testTenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentUserInTenant_ShouldReturnFalse_WhenNoTenantClaim()
    {
        // Arrange
        var testTenantId = "tenant-999";
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@example.com")
            // No tenant_id claim
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.IsCurrentUserInTenant(testTenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("manager")]
    [InlineData("user")]
    public void IsCurrentUserInRole_ShouldReturnTrue_WhenUserHasRole(string roleName)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@example.com"),
            new(ClaimTypes.Role, roleName)
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.IsCurrentUserInRole(roleName);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCurrentUserInRole_ShouldReturnFalse_WhenUserDoesNotHaveRole()
    {
        // Arrange
        var userRole = "user";
        var testRole = "admin";
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@example.com"),
            new(ClaimTypes.Role, userRole)
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.IsCurrentUserInRole(testRole);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsCurrentUserInRole_ShouldReturnFalse_WhenHttpContextIsNull()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _tenantService.IsCurrentUserInRole("admin");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetUserClaims_ShouldReturnAllClaims_WhenUserHasClaims()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test@example.com"),
            new(ClaimTypes.NameIdentifier, "user-123"),
            new("tenant_id", "tenant-456"),
            new(ClaimTypes.Role, "admin")
        };
        
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetUserClaims();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        result.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "test@example.com");
        result.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user-123");
        result.Should().Contain(c => c.Type == "tenant_id" && c.Value == "tenant-456");
        result.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "admin");
    }

    [Fact]
    public void GetUserClaims_ShouldReturnEmptyList_WhenHttpContextIsNull()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _tenantService.GetUserClaims();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetUserClaims_ShouldReturnEmptyList_WhenUserIsNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            User = null
        };
        
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetUserClaims();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}