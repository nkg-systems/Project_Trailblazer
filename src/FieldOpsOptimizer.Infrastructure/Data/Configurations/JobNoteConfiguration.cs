using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Infrastructure.Data.Configurations;

public class JobNoteConfiguration : IEntityTypeConfiguration<JobNote>
{
    public void Configure(EntityTypeBuilder<JobNote> builder)
    {
        builder.HasKey(jn => jn.Id);

        // Basic properties
        builder.Property(jn => jn.Content)
            .IsRequired()
            .HasMaxLength(JobNote.MaxContentLength)
            .HasComment("Note content (encrypted in production)");

        builder.Property(jn => jn.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasComment("Type of note determining visibility and purpose");

        builder.Property(jn => jn.IsCustomerVisible)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment("Whether this note should be visible to customers");

        builder.Property(jn => jn.IsSensitive)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment("Whether this note contains sensitive information");

        // Author information
        builder.Property(jn => jn.AuthorUserId)
            .IsRequired()
            .HasComment("ID of the user who created the note");

        builder.Property(jn => jn.AuthorName)
            .IsRequired()
            .HasMaxLength(JobNote.MaxAuthorNameLength)
            .HasComment("Name of the user who created the note (cached for performance)");

        builder.Property(jn => jn.AuthorRole)
            .HasMaxLength(100)
            .HasComment("Role/position of the note author at time of creation");

        // Foreign keys and tenant isolation
        builder.Property(jn => jn.ServiceJobId)
            .IsRequired()
            .HasComment("ID of the service job this note belongs to");

        builder.Property(jn => jn.TenantId)
            .IsRequired()
            .HasMaxLength(100)
            .HasComment("Tenant ID for multi-tenant isolation");

        // Audit trail properties
        builder.Property(jn => jn.IpAddress)
            .HasMaxLength(JobNote.MaxIpAddressLength)
            .HasComment("IP address of the user who created the note (for audit trail)");

        builder.Property(jn => jn.UserAgent)
            .HasMaxLength(JobNote.MaxUserAgentLength)
            .HasComment("User agent of the client that created the note");

        builder.Property(jn => jn.SessionId)
            .HasMaxLength(128)
            .HasComment("Session ID when the note was created");

        // Soft delete properties
        builder.Property(jn => jn.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment("Whether this note has been soft deleted");

        builder.Property(jn => jn.DeletedAt)
            .HasComment("When the note was soft deleted");

        builder.Property(jn => jn.DeletedByUserId)
            .HasComment("User ID who deleted the note");

        builder.Property(jn => jn.DeletedByUserName)
            .HasMaxLength(JobNote.MaxAuthorNameLength)
            .HasComment("Name of user who deleted the note");

        builder.Property(jn => jn.DeletionReason)
            .HasMaxLength(500)
            .HasComment("Reason for deletion");

        // Concurrency control
        builder.Property(jn => jn.RowVersion)
            .IsRequired()
            .IsRowVersion()
            .HasComment("Concurrency token for optimistic locking");

        // Configure relationships
        builder.HasOne(jn => jn.ServiceJob)
            .WithMany() // ServiceJob doesn't have a direct navigation property for notes yet
            .HasForeignKey(jn => jn.ServiceJobId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_JobNotes_ServiceJobs");

        // Indexes for performance and tenant isolation
        
        // Primary index for job notes by service job (most common query)
        builder.HasIndex(jn => new { jn.ServiceJobId, jn.TenantId, jn.IsDeleted })
            .HasDatabaseName("IX_JobNotes_ServiceJob_Tenant_Deleted")
            .HasFilter("IsDeleted = 0");

        // Index for tenant isolation (security critical)
        builder.HasIndex(jn => jn.TenantId)
            .HasDatabaseName("IX_JobNotes_TenantId");

        // Index for author queries
        builder.HasIndex(jn => new { jn.AuthorUserId, jn.TenantId, jn.IsDeleted })
            .HasDatabaseName("IX_JobNotes_Author_Tenant_Deleted")
            .HasFilter("IsDeleted = 0");

        // Index for customer-visible notes (for customer portals)
        builder.HasIndex(jn => new { jn.ServiceJobId, jn.IsCustomerVisible, jn.IsSensitive, jn.IsDeleted })
            .HasDatabaseName("IX_JobNotes_CustomerVisible")
            .HasFilter("IsCustomerVisible = 1 AND IsSensitive = 0 AND IsDeleted = 0");

        // Index for note type filtering
        builder.HasIndex(jn => new { jn.Type, jn.TenantId, jn.IsDeleted })
            .HasDatabaseName("IX_JobNotes_Type_Tenant_Deleted")
            .HasFilter("IsDeleted = 0");

        // Index for audit queries (by creation time)
        builder.HasIndex(jn => new { jn.CreatedAt, jn.TenantId })
            .HasDatabaseName("IX_JobNotes_CreatedAt_Tenant");

        // Index for soft delete management
        builder.HasIndex(jn => new { jn.IsDeleted, jn.DeletedAt, jn.TenantId })
            .HasDatabaseName("IX_JobNotes_SoftDelete")
            .HasFilter("IsDeleted = 1");

        // Check constraints simplified for compatibility
        // Note: Business rule validation is primarily handled in domain logic

        // Table comment
        builder.ToTable("JobNotes", tb => tb.HasComment("Notes attached to service jobs with full audit trail and security features"));
    }
}