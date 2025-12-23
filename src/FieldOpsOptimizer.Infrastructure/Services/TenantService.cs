using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FieldOpsOptimizer.Application.Common.Interfaces;
using System.Security.Claims;

namespace FieldOpsOptimizer.Infrastructure.Services;

/// <summary>
/// Service for managing multi-tenant context and operations
/// </summary>
public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TenantService> _logger;
    private string? _currentTenantId;

    public TenantService(IHttpContextAccessor httpContextAccessor, ILogger<TenantService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current tenant ID from the request context
    /// </summary>
    /// <returns>Current tenant ID or null if not in a tenant context</returns>
    public string? GetCurrentTenantId()
    {
        if (!string.IsNullOrEmpty(_currentTenantId))
        {
            return _currentTenantId;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        // Security: Only accept tenant ID from JWT claims for authenticated users
        // This prevents tenant ID spoofing via headers, query parameters, or subdomains
        
        // 1. From user claims (PRIMARY AND ONLY SOURCE for authenticated requests)
        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = user.FindFirst("tenant_id");
            if (tenantClaim != null && !string.IsNullOrEmpty(tenantClaim.Value))
            {
                _logger.LogDebug("Tenant ID found in user claims: {TenantId}", tenantClaim.Value);
                return tenantClaim.Value;
            }
            
            // If user is authenticated but has no tenant claim, log a warning
            _logger.LogWarning("Authenticated user has no tenant_id claim. User: {UserId}", 
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown");
        }

        _logger.LogDebug("No tenant ID found in request context");
        return null;
    }

    /// <summary>
    /// Gets the current tenant information
    /// </summary>
    /// <returns>Current tenant information or null if not in a tenant context</returns>
    public TenantInfo? GetCurrentTenant()
    {
        var tenantId = GetCurrentTenantId();
        if (string.IsNullOrEmpty(tenantId))
        {
            return null;
        }

        // TODO: In a real application, you would fetch this from a database or cache
        // For now, return a basic tenant info
        return new TenantInfo
        {
            Id = tenantId,
            Name = $"Tenant {tenantId}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30), // Placeholder
            EnabledFeatures = new List<string> { "JobManagement", "TechnicianTracking", "Reporting" }
        };
    }

    /// <summary>
    /// Validates if the current user has access to the specified tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID to validate access for</param>
    /// <returns>True if user has access to the tenant</returns>
    public Task<bool> HasTenantAccessAsync(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return Task.FromResult(false);
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(false);
        }

        // TODO: In a real application, you would check user permissions against a database
        // This might involve checking user-tenant relationships, roles, etc.
        
        // For now, we'll do basic validation:
        // 1. If the user has a tenant claim, it must match
        var userTenantClaim = httpContext.User.FindFirst("tenant_id");
        if (userTenantClaim != null)
        {
            var hasAccess = userTenantClaim.Value == tenantId;
            _logger.LogDebug("Tenant access validation for user {UserId} to tenant {TenantId}: {HasAccess}", 
                httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, 
                tenantId, 
                hasAccess);
            return Task.FromResult(hasAccess);
        }

        // 2. If no tenant claim, check if user is a system admin
        var isSystemAdmin = httpContext.User.IsInRole("SystemAdmin");
        if (isSystemAdmin)
        {
            _logger.LogDebug("System admin user has access to tenant {TenantId}", tenantId);
            return Task.FromResult(true);
        }

        // 3. Default to no access
        _logger.LogWarning("User {UserId} denied access to tenant {TenantId}", 
            httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, 
            tenantId);
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets all tenants that the current user has access to
    /// </summary>
    /// <returns>Collection of accessible tenants</returns>
    public Task<IEnumerable<TenantInfo>> GetAccessibleTenantsAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(Enumerable.Empty<TenantInfo>());
        }

        // TODO: In a real application, you would fetch this from a database
        // This might involve joining user-tenant relationships
        
        var tenants = new List<TenantInfo>();

        // If user has a specific tenant claim, return only that tenant
        var userTenantClaim = httpContext.User.FindFirst("tenant_id");
        if (userTenantClaim != null && !string.IsNullOrEmpty(userTenantClaim.Value))
        {
            tenants.Add(new TenantInfo
            {
                Id = userTenantClaim.Value,
                Name = $"Tenant {userTenantClaim.Value}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                EnabledFeatures = new List<string> { "JobManagement", "TechnicianTracking", "Reporting" }
            });
        }

        // If user is a system admin, return all tenants (placeholder)
        if (httpContext.User.IsInRole("SystemAdmin"))
        {
            tenants.AddRange(new[]
            {
                new TenantInfo
                {
                    Id = "tenant1",
                    Name = "ABC Field Services",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-60),
                    EnabledFeatures = new List<string> { "JobManagement", "TechnicianTracking", "Reporting", "Analytics" }
                },
                new TenantInfo
                {
                    Id = "tenant2",
                    Name = "XYZ Maintenance Co",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    EnabledFeatures = new List<string> { "JobManagement", "TechnicianTracking" }
                }
            });
        }

        _logger.LogDebug("User {UserId} has access to {TenantCount} tenants", 
            httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, 
            tenants.Count);

        return Task.FromResult<IEnumerable<TenantInfo>>(tenants);
    }

    /// <summary>
    /// Sets the tenant context for the current request
    /// </summary>
    /// <param name="tenantId">Tenant ID to set as current</param>
    public void SetCurrentTenant(string tenantId)
    {
        _currentTenantId = tenantId;
        _logger.LogDebug("Current tenant set to: {TenantId}", tenantId);
    }
}