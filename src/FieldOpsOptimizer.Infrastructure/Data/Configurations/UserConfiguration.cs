using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for User entity
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);
            
        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(255);
            
        builder.Property(u => u.TenantId)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(u => u.RefreshToken)
            .HasMaxLength(500);
            
        builder.Property(u => u.RefreshTokenExpiryTime)
            .HasColumnType("timestamp");
            
        builder.Property(u => u.LastLoginAt)
            .HasColumnType("timestamp");
            
        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp");
            
        builder.Property(u => u.UpdatedAt)
            .HasColumnType("timestamp");

        // Configure roles collection as comma-separated string
        builder.Property(u => u.Roles)
            .HasConversion(
                roles => string.Join(',', roles.Select(r => r.ToString())),
                value => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(r => Enum.Parse<UserRole>(r))
                            .ToList().AsReadOnly())
            .HasMaxLength(500);

        // Create indexes for performance
        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasDatabaseName("IX_Users_Username");
            
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");
            
        builder.HasIndex(u => new { u.TenantId, u.Username })
            .IsUnique()
            .HasDatabaseName("IX_Users_TenantId_Username");
            
        builder.HasIndex(u => u.RefreshToken)
            .HasDatabaseName("IX_Users_RefreshToken");

        // Configure relationship with Technician
        builder.HasOne<Technician>()
            .WithMany()
            .HasForeignKey(u => u.TechnicianId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure table name
        builder.ToTable("Users");
    }
}
