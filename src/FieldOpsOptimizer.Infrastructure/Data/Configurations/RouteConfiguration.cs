using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FieldOpsOptimizer.Domain.Entities;

namespace FieldOpsOptimizer.Infrastructure.Data.Configurations;

public class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.OptimizationObjective)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(r => r.TotalDistanceKm)
            .HasPrecision(10, 3);

        // Relationships
        builder.HasOne(r => r.AssignedTechnician)
            .WithMany()
            .HasForeignKey(r => r.AssignedTechnicianId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<RouteStop>()
            .WithOne()
            .HasForeignKey("RouteId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.AssignedTechnicianId);
        builder.HasIndex(r => r.ScheduledDate);
        builder.HasIndex(r => r.Status);
    }
}

public class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> builder)
    {
        builder.HasKey(rs => new { rs.JobId, RouteId = EF.Property<Guid>(rs, "RouteId") });

        builder.Property(rs => rs.DistanceFromPreviousKm)
            .HasPrecision(10, 3);

        // Relationships
        builder.HasOne(rs => rs.Job)
            .WithMany()
            .HasForeignKey(rs => rs.JobId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(rs => rs.SequenceOrder);
        builder.HasIndex(rs => rs.EstimatedArrival);
    }
}
