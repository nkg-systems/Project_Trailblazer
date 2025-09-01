using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.ValueObjects;
using System.Text.Json;

namespace FieldOpsOptimizer.Infrastructure.Data.Configurations;

public class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.EmployeeId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Phone)
            .HasMaxLength(20);

        builder.Property(t => t.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.HourlyRate)
            .HasPrecision(18, 2);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Configure Skills as JSON
        builder.Property(t => t.Skills)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnType("jsonb");

        // Configure WorkingHours as JSON
        builder.Property(t => t.WorkingHours)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<WorkingHours>>(v, (JsonSerializerOptions?)null) ?? new List<WorkingHours>())
            .HasColumnType("jsonb");

        // Configure Address as owned entity
        builder.OwnsOne(t => t.HomeAddress, address =>
        {
            address.Property(a => a.Street).HasMaxLength(200);
            address.Property(a => a.Unit).HasMaxLength(50);
            address.Property(a => a.City).HasMaxLength(100);
            address.Property(a => a.State).HasMaxLength(50);
            address.Property(a => a.PostalCode).HasMaxLength(20);
            address.Property(a => a.Country).HasMaxLength(50);
            
            address.OwnsOne(a => a.Coordinate, coord =>
            {
                coord.Property(c => c.Latitude).HasPrecision(10, 7);
                coord.Property(c => c.Longitude).HasPrecision(10, 7);
            });
        });

        // Configure CurrentLocation as owned entity
        builder.OwnsOne(t => t.CurrentLocation, coord =>
        {
            coord.Property(c => c.Latitude).HasPrecision(10, 7);
            coord.Property(c => c.Longitude).HasPrecision(10, 7);
        });

        // Indexes
        builder.HasIndex(t => new { t.EmployeeId, t.TenantId })
            .IsUnique();

        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.Email);
        builder.HasIndex(t => t.Status);
    }
}
