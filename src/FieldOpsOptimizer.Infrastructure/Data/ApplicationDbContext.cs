using Microsoft.EntityFrameworkCore;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Infrastructure.Data.Configurations;
using FieldOpsOptimizer.Application.Common.Interfaces;

namespace FieldOpsOptimizer.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    private readonly ITenantService? _tenantService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantService tenantService)
        : base(options)
    {
        _tenantService = tenantService;
    }

    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<ServiceJob> ServiceJobs => Set<ServiceJob>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<User> Users => Set<User>();
    
    // New entities for core data features
    public DbSet<JobNote> JobNotes => Set<JobNote>();
    public DbSet<JobStatusHistory> JobStatusHistory => Set<JobStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply individual entity configurations
        modelBuilder.ApplyConfiguration(new TechnicianConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceJobConfiguration());
        modelBuilder.ApplyConfiguration(new RouteConfiguration());
        modelBuilder.ApplyConfiguration(new RouteStopConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        
        // New configurations for core data features
        modelBuilder.ApplyConfiguration(new JobNoteConfiguration());
        modelBuilder.ApplyConfiguration(new JobStatusHistoryConfiguration());

        // Apply global configurations for security, performance, and multi-tenancy
        var currentTenantId = _tenantService?.GetCurrentTenantId();
        modelBuilder.ConfigureGlobalFilters(currentTenantId);
        modelBuilder.ConfigureSecuritySettings();
        
        base.OnModelCreating(modelBuilder);
    }
}
