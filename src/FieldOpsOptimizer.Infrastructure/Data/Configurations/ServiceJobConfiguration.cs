using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FieldOpsOptimizer.Domain.Entities;
using System.Text.Json;

namespace FieldOpsOptimizer.Infrastructure.Data.Configurations;

public class ServiceJobConfiguration : IEntityTypeConfiguration<ServiceJob>
{
    public void Configure(EntityTypeBuilder<ServiceJob> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.JobNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(j => j.CustomerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.CustomerPhone)
            .HasMaxLength(20);

        builder.Property(j => j.CustomerEmail)
            .HasMaxLength(200);

        builder.Property(j => j.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(j => j.Notes)
            .HasMaxLength(2000);

        builder.Property(j => j.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(j => j.Priority)
            .HasConversion<int>();

        builder.Property(j => j.EstimatedRevenue)
            .HasPrecision(18, 2);

        // Configure ServiceAddress as owned entity
        builder.OwnsOne(j => j.ServiceAddress, address =>
        {
            address.Property(a => a.Street)
                .IsRequired()
                .HasMaxLength(200);
                
            address.Property(a => a.Unit).HasMaxLength(50);
            address.Property(a => a.City)
                .IsRequired()
                .HasMaxLength(100);
                
            address.Property(a => a.State)
                .IsRequired()
                .HasMaxLength(50);
                
            address.Property(a => a.PostalCode)
                .IsRequired()
                .HasMaxLength(20);
                
            address.Property(a => a.Country)
                .HasMaxLength(50)
                .HasDefaultValue("US");
            
            address.OwnsOne(a => a.Coordinate, coord =>
            {
                coord.Property(c => c.Latitude).HasPrecision(10, 7);
                coord.Property(c => c.Longitude).HasPrecision(10, 7);
            });
        });

        // Configure RequiredSkills as JSON
        builder.Property(j => j.RequiredSkills)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnType("jsonb");

        // Configure Tags as JSON
        builder.Property(j => j.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnType("jsonb");

        // Relationships
        builder.HasOne(j => j.AssignedTechnician)
            .WithMany()
            .HasForeignKey(j => j.AssignedTechnicianId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(j => j.Route)
            .WithMany()
            .HasForeignKey(j => j.RouteId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(j => new { j.JobNumber, j.TenantId })
            .IsUnique();

        builder.HasIndex(j => j.TenantId);
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.Priority);
        builder.HasIndex(j => j.ScheduledDate);
        builder.HasIndex(j => j.AssignedTechnicianId);
        builder.HasIndex(j => j.RouteId);
    }
}
