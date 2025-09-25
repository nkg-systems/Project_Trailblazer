namespace FieldOpsOptimizer.Application.Common.Interfaces;

/// <summary>
/// Service for managing multi-tenant context and operations
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Gets the current tenant ID from the request context
    /// </summary>
    /// <returns>Current tenant ID or null if not in a tenant context</returns>
    string? GetCurrentTenantId();

    /// <summary>
    /// Gets the current tenant information
    /// </summary>
    /// <returns>Current tenant information or null if not in a tenant context</returns>
    TenantInfo? GetCurrentTenant();

    /// <summary>
    /// Validates if the current user has access to the specified tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID to validate access for</param>
    /// <returns>True if user has access to the tenant</returns>
    Task<bool> HasTenantAccessAsync(string tenantId);

    /// <summary>
    /// Gets all tenants that the current user has access to
    /// </summary>
    /// <returns>Collection of accessible tenants</returns>
    Task<IEnumerable<TenantInfo>> GetAccessibleTenantsAsync();

    /// <summary>
    /// Sets the tenant context for the current request
    /// </summary>
    /// <param name="tenantId">Tenant ID to set as current</param>
    void SetCurrentTenant(string tenantId);
}

/// <summary>
/// Information about a tenant
/// </summary>
public class TenantInfo
{
    /// <summary>
    /// Unique tenant identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the tenant
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the tenant is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Connection string for tenant-specific database (if using database-per-tenant)
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Tenant configuration settings
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// When the tenant was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Maximum number of users allowed for this tenant
    /// </summary>
    public int? MaxUsers { get; set; }

    /// <summary>
    /// Maximum number of jobs allowed for this tenant
    /// </summary>
    public int? MaxJobs { get; set; }

    /// <summary>
    /// Features enabled for this tenant
    /// </summary>
    public List<string> EnabledFeatures { get; set; } = new();
}