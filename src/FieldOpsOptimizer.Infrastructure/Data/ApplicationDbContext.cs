using Microsoft.EntityFrameworkCore;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Infrastructure.Data.Configurations;

namespace FieldOpsOptimizer.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<ServiceJob> ServiceJobs => Set<ServiceJob>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TechnicianConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceJobConfiguration());
        modelBuilder.ApplyConfiguration(new RouteConfiguration());
        modelBuilder.ApplyConfiguration(new RouteStopConfiguration());

        // Global query filters for multi-tenancy (if needed)
        // This would filter by tenant automatically
        
        base.OnModelCreating(modelBuilder);
    }
}
