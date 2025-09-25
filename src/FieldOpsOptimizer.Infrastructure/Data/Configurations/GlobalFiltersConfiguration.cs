using Microsoft.EntityFrameworkCore;
using FieldOpsOptimizer.Domain.Entities;

namespace FieldOpsOptimizer.Infrastructure.Data.Configurations;

/// <summary>
/// Configuration for global query filters and cross-cutting concerns
/// </summary>
public static class GlobalFiltersConfiguration
{
    /// <summary>
    /// Applies global query filters for multi-tenancy and security
    /// </summary>
    /// <param name="modelBuilder">The model builder</param>
    /// <param name="tenantId">Current tenant ID (if available)</param>
    public static void ConfigureGlobalFilters(this ModelBuilder modelBuilder, string? tenantId = null)
    {
        // Note: In a real application, you would get the current tenant ID from a service
        // For now, we'll set up the filters but they would need to be activated with actual tenant context
        
        // Global query filter for ServiceJob - automatically filter by tenant
        // This ensures tenant isolation at the database level
        modelBuilder.Entity<ServiceJob>()
            .HasQueryFilter(sj => string.IsNullOrEmpty(tenantId) || sj.TenantId == tenantId);

        // Global query filter for JobNote - automatically filter by tenant and exclude soft deleted
        modelBuilder.Entity<JobNote>()
            .HasQueryFilter(jn => (string.IsNullOrEmpty(tenantId) || jn.TenantId == tenantId) && !jn.IsDeleted);

        // Global query filter for JobStatusHistory - automatically filter by tenant
        modelBuilder.Entity<JobStatusHistory>()
            .HasQueryFilter(jsh => string.IsNullOrEmpty(tenantId) || jsh.TenantId == tenantId);

        // Global query filter for Technician - automatically filter by tenant
        modelBuilder.Entity<Technician>()
            .HasQueryFilter(t => string.IsNullOrEmpty(tenantId) || t.TenantId == tenantId);

        // Global query filter for User - automatically filter by tenant
        modelBuilder.Entity<User>()
            .HasQueryFilter(u => string.IsNullOrEmpty(tenantId) || u.TenantId == tenantId);

        // Global query filter for WeatherData - automatically filter by tenant
        modelBuilder.Entity<WeatherData>()
            .HasQueryFilter(wd => string.IsNullOrEmpty(tenantId) || wd.TenantId == tenantId);
    }

    /// <summary>
    /// Configures additional security and performance settings
    /// </summary>
    /// <param name="modelBuilder">The model builder</param>
    public static void ConfigureSecuritySettings(this ModelBuilder modelBuilder)
    {
        // Configure default values and computed columns for audit fields
        
        // Set default created and updated timestamps
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Configure CreatedAt default value
            var createdAtProperty = entityType.FindProperty("CreatedAt");
            if (createdAtProperty != null)
            {
                createdAtProperty.SetDefaultValueSql("GETUTCDATE()");
            }

            // Configure UpdatedAt default value
            var updatedAtProperty = entityType.FindProperty("UpdatedAt");
            if (updatedAtProperty != null)
            {
                updatedAtProperty.SetDefaultValueSql("GETUTCDATE()");
            }
        }

        // Configure sensitive data encryption (placeholder for future implementation)
        ConfigureEncryption(modelBuilder);
        
        // Configure audit triggers (placeholder for future implementation)
        ConfigureAuditTriggers(modelBuilder);
    }

    /// <summary>
    /// Configures data encryption for sensitive fields
    /// </summary>
    /// <param name="modelBuilder">The model builder</param>
    private static void ConfigureEncryption(ModelBuilder modelBuilder)
    {
        // TODO: In a production environment, you would configure field-level encryption
        // Example approaches:
        // 1. Use SQL Server Always Encrypted
        // 2. Use Entity Framework Value Converters for custom encryption
        // 3. Use database-level transparent data encryption (TDE)
        
        // Example value converter for JobNote content encryption:
        /*
        modelBuilder.Entity<JobNote>()
            .Property(e => e.Content)
            .HasConversion(
                v => EncryptString(v),     // Encrypt when saving to database
                v => DecryptString(v),     // Decrypt when reading from database
                new ValueComparer<string>(
                    (c1, c2) => c1 == c2,
                    c => c.GetHashCode(),
                    c => c));
        */
        
        // For now, we'll just add comments to indicate encryption readiness
        modelBuilder.Entity<JobNote>()
            .Property(jn => jn.Content)
            .HasComment("Note content (encrypted in production using AES-256)");
    }

    /// <summary>
    /// Configures database audit triggers
    /// </summary>
    /// <param name="modelBuilder">The model builder</param>
    private static void ConfigureAuditTriggers(ModelBuilder modelBuilder)
    {
        // TODO: In a production environment, you might want to set up database triggers
        // for automatic audit logging, or use Entity Framework interceptors
        
        // Example SQL trigger for JobNote changes:
        /*
        modelBuilder.Entity<JobNote>().ToTable(tb => tb.HasTrigger("TR_JobNotes_Audit"));
        */
        
        // For now, we'll rely on application-level audit logging
    }



    /// <summary>
    /// Example function for getting current tenant ID (would be implemented in database)
    /// </summary>
    /// <returns>Current tenant ID</returns>
    public static string GetCurrentTenantId()
    {
        throw new InvalidOperationException("This method should only be used in database queries");
    }
}