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

        // Enhanced Availability Properties
        builder.Property(t => t.IsCurrentlyAvailable)
            .IsRequired()
            .HasDefaultValue(true)
            .HasComment("Whether the technician is currently available for new job assignments");

        builder.Property(t => t.AvailabilityChangedAt)
            .HasComment("When the technician's availability status was last changed");

        builder.Property(t => t.AvailabilityChangedByUserId)
            .HasComment("User ID who last changed the technician's availability");

        builder.Property(t => t.AvailabilityChangedByUserName)
            .HasMaxLength(200)
            .HasComment("Name of user who last changed the technician's availability");

        builder.Property(t => t.UnavailabilityReason)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasComment("Reason for current unavailability (if applicable)");

        builder.Property(t => t.AvailabilityNotes)
            .HasMaxLength(500)
            .HasComment("Additional notes about current availability status");

        builder.Property(t => t.ExpectedAvailableAt)
            .HasComment("Expected time when technician will be available again (if currently unavailable)");

        builder.Property(t => t.CanTakeEmergencyJobs)
            .IsRequired()
            .HasDefaultValue(true)
            .HasComment("Whether the technician can be assigned emergency jobs even when unavailable");

        builder.Property(t => t.MaxConcurrentJobs)
            .IsRequired()
            .HasDefaultValue(3)
            .HasComment("Maximum number of concurrent jobs this technician can handle");

        builder.Property(t => t.RowVersion)
            .IsRequired()
            .IsRowVersion()
            .HasComment("Row version for optimistic concurrency control");

        // Indexes
        builder.HasIndex(t => new { t.EmployeeId, t.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_Technicians_EmployeeId_TenantId_Unique");

        builder.HasIndex(t => t.TenantId)
            .HasDatabaseName("IX_Technicians_TenantId");

        builder.HasIndex(t => t.Email)
            .HasDatabaseName("IX_Technicians_Email");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("IX_Technicians_Status");

        // Enhanced availability indexes
        builder.HasIndex(t => new { t.IsCurrentlyAvailable, t.Status, t.TenantId })
            .HasDatabaseName("IX_Technicians_Available_Status_Tenant")
            .HasFilter("Status = 'Active'");

        builder.HasIndex(t => new { t.UnavailabilityReason, t.ExpectedAvailableAt, t.TenantId })
            .HasDatabaseName("IX_Technicians_Unavailable_Expected_Tenant")
            .HasFilter("IsCurrentlyAvailable = 0 AND ExpectedAvailableAt IS NOT NULL");

        builder.HasIndex(t => new { t.CanTakeEmergencyJobs, t.Status, t.TenantId })
            .HasDatabaseName("IX_Technicians_Emergency_Status_Tenant")
            .HasFilter("CanTakeEmergencyJobs = 1 AND Status = 'Active'");

        builder.HasIndex(t => new { t.MaxConcurrentJobs, t.IsCurrentlyAvailable, t.TenantId })
            .HasDatabaseName("IX_Technicians_Capacity_Available_Tenant")
            .HasFilter("IsCurrentlyAvailable = 1");

        builder.HasIndex(t => new { t.AvailabilityChangedAt, t.TenantId })
            .HasDatabaseName("IX_Technicians_AvailabilityChanged_Tenant");

        // Check constraints simplified for compatibility
        // Note: Business rule validation is primarily handled in domain logic
    }
}
