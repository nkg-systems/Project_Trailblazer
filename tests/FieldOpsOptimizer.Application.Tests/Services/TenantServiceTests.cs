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
    public void GetCurrentTenantId_ShouldReturnTenantId_FromHeader()
    {
        // Arrange
        var expectedTenantId = "tenant-header";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-ID"] = expectedTenantId;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentTenantId();

        // Assert
        result.Should().Be(expectedTenantId);
    }

    [Fact]
    public void GetCurrentTenantId_ShouldReturnTenantId_FromClaims()
    {
        // Arrange
        var expectedTenantId = "tenant-claims";
        var claims = new List<Claim>
        {
            new("tenant_id", expectedTenantId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType"); // Must have authenticationType to be considered authenticated
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentTenantId();

        // Assert
        result.Should().Be(expectedTenantId);
    }

    [Fact]
    public void GetCurrentTenantId_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _tenantService.GetCurrentTenantId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentTenant_ShouldReturnTenantInfo_WhenTenantResolved()
    {
        // Arrange
        var expectedTenantId = "tenant-123";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-ID"] = expectedTenantId;
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var tenant = _tenantService.GetCurrentTenant();

        // Assert
        tenant.Should().NotBeNull();
        tenant!.Id.Should().Be(expectedTenantId);
        tenant.Name.Should().Be($"Tenant {expectedTenantId}");
        tenant.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task HasTenantAccessAsync_ShouldReturnTrue_WhenClaimMatches()
    {
        // Arrange
        var expectedTenantId = "tenant-123";
        var claims = new List<Claim>
        {
            new("tenant_id", expectedTenantId),
            new(ClaimTypes.NameIdentifier, "user-1")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var hasAccess = await _tenantService.HasTenantAccessAsync(expectedTenantId);

        // Assert
        hasAccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessibleTenantsAsync_ShouldReturnList_WhenUserAuthenticated()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-1"),
            new("tenant_id", "tenant-abc")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var tenants = await _tenantService.GetAccessibleTenantsAsync();

        // Assert
        tenants.Should().NotBeNull();
        tenants.Should().NotBeEmpty();
        tenants!.First().Id.Should().Be("tenant-abc");
    }

    [Fact]
    public void SetCurrentTenant_ShouldOverrideTenantContext()
    {
        // Arrange
        var expectedTenantId = "override-tenant";
        var httpContext = new DefaultHttpContext();
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        _tenantService.SetCurrentTenant(expectedTenantId);

        // Assert
        _tenantService.GetCurrentTenantId().Should().Be(expectedTenantId);
    }
}