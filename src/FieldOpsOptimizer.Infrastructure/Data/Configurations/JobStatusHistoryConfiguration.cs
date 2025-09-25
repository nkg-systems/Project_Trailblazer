using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Infrastructure.Data.Configurations;

public class JobStatusHistoryConfiguration : IEntityTypeConfiguration<JobStatusHistory>
{
    public void Configure(EntityTypeBuilder<JobStatusHistory> builder)
    {
        builder.HasKey(jsh => jsh.Id);

        // Basic properties
        builder.Property(jsh => jsh.ServiceJobId)
            .IsRequired()
            .HasComment("ID of the service job whose status changed");

        builder.Property(jsh => jsh.JobNumber)
            .IsRequired()
            .HasMaxLength(100)
            .HasComment("Job number for easier identification in logs and reports");

        builder.Property(jsh => jsh.TenantId)
            .IsRequired()
            .HasMaxLength(100)
            .HasComment("Tenant ID for multi-tenant isolation");

        // Status information
        builder.Property(jsh => jsh.FromStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasComment("The status before the change");

        builder.Property(jsh => jsh.ToStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasComment("The status after the change");

        builder.Property(jsh => jsh.ChangedAt)
            .IsRequired()
            .HasComment("When the status change occurred (separate from audit timestamps)");

        // User information
        builder.Property(jsh => jsh.ChangedByUserId)
            .IsRequired()
            .HasComment("ID of the user who made the status change");

        builder.Property(jsh => jsh.ChangedByUserName)
            .IsRequired()
            .HasMaxLength(JobStatusHistory.MaxUserNameLength)
            .HasComment("Name of the user who made the status change (cached for performance)");

        builder.Property(jsh => jsh.ChangedByUserRole)
            .HasMaxLength(100)
            .HasComment("Role of the user at the time of the change");

        builder.Property(jsh => jsh.Reason)
            .HasMaxLength(JobStatusHistory.MaxReasonLength)
            .HasComment("Optional reason or comment for the status change");

        // Change metadata
        builder.Property(jsh => jsh.IsAutomaticChange)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment("Whether this was an automatic status change (vs manual)");

        builder.Property(jsh => jsh.ChangeSource)
            .HasMaxLength(100)
            .HasComment("Source system or component that triggered the change");

        // Audit trail properties
        builder.Property(jsh => jsh.IpAddress)
            .HasMaxLength(JobStatusHistory.MaxIpAddressLength)
            .HasComment("IP address from which the change was made");

        builder.Property(jsh => jsh.UserAgent)
            .HasMaxLength(JobStatusHistory.MaxUserAgentLength)
            .HasComment("User agent of the client that made the change");

        builder.Property(jsh => jsh.SessionId)
            .HasMaxLength(128)
            .HasComment("Session ID when the change was made");

        // Performance and business metrics
        builder.Property(jsh => jsh.PreviousStatusDurationMinutes)
            .HasComment("Duration the job was in the previous status (in minutes)");

        builder.Property(jsh => jsh.TriggeredNotifications)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment("Whether this status change triggered any business rules or notifications");

        builder.Property(jsh => jsh.ValidationWarnings)
            .HasMaxLength(1000)
            .HasComment("Any validation warnings that occurred during the status change");

        builder.Property(jsh => jsh.AppliedBusinessRules)
            .HasMaxLength(1000)
            .HasComment("Business rules that were applied during this status change");

        // Configure relationships
        builder.HasOne(jsh => jsh.ServiceJob)
            .WithMany() // ServiceJob doesn't have a direct navigation property for status history yet
            .HasForeignKey(jsh => jsh.ServiceJobId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_JobStatusHistory_ServiceJobs");

        // Indexes for performance, security, and audit queries

        // Primary index for job status history by service job (most common query)
        builder.HasIndex(jsh => new { jsh.ServiceJobId, jsh.ChangedAt })
            .HasDatabaseName("IX_JobStatusHistory_ServiceJob_ChangedAt");

        // Index for tenant isolation (security critical)
        builder.HasIndex(jsh => jsh.TenantId)
            .HasDatabaseName("IX_JobStatusHistory_TenantId");

        // Index for audit queries by user
        builder.HasIndex(jsh => new { jsh.ChangedByUserId, jsh.TenantId, jsh.ChangedAt })
            .HasDatabaseName("IX_JobStatusHistory_User_Tenant_ChangedAt");

        // Index for status transition analysis
        builder.HasIndex(jsh => new { jsh.FromStatus, jsh.ToStatus, jsh.TenantId, jsh.ChangedAt })
            .HasDatabaseName("IX_JobStatusHistory_StatusTransition_Tenant_ChangedAt");

        // Index for automatic vs manual changes analysis
        builder.HasIndex(jsh => new { jsh.IsAutomaticChange, jsh.TenantId, jsh.ChangedAt })
            .HasDatabaseName("IX_JobStatusHistory_AutoChange_Tenant_ChangedAt");

        // Index for business rule tracking
        builder.HasIndex(jsh => new { jsh.TriggeredNotifications, jsh.TenantId, jsh.ChangedAt })
            .HasDatabaseName("IX_JobStatusHistory_Notifications_Tenant_ChangedAt")
            .HasFilter("TriggeredNotifications = 1");

        // Index for performance analysis (status duration)
        builder.HasIndex(jsh => new { jsh.PreviousStatusDurationMinutes, jsh.FromStatus, jsh.TenantId })
            .HasDatabaseName("IX_JobStatusHistory_Duration_Status_Tenant")
            .HasFilter("PreviousStatusDurationMinutes IS NOT NULL");

        // Index for audit trail by time range
        builder.HasIndex(jsh => new { jsh.ChangedAt, jsh.TenantId })
            .HasDatabaseName("IX_JobStatusHistory_ChangedAt_Tenant");

        // Index for source system analysis
        builder.HasIndex(jsh => new { jsh.ChangeSource, jsh.IsAutomaticChange, jsh.TenantId })
            .HasDatabaseName("IX_JobStatusHistory_Source_Auto_Tenant")
            .HasFilter("ChangeSource IS NOT NULL");

        // Index for validation warnings and business rules
        builder.HasIndex(jsh => new { jsh.ValidationWarnings, jsh.AppliedBusinessRules, jsh.TenantId })
            .HasDatabaseName("IX_JobStatusHistory_Warnings_Rules_Tenant")
            .HasFilter("ValidationWarnings IS NOT NULL OR AppliedBusinessRules IS NOT NULL");

        // Composite index for job number lookups (useful for support)
        builder.HasIndex(jsh => new { jsh.JobNumber, jsh.TenantId, jsh.ChangedAt })
            .HasDatabaseName("IX_JobStatusHistory_JobNumber_Tenant_ChangedAt");

        // Check constraints simplified for compatibility
        // Note: Business rule validation is primarily handled in domain logic

        // Table comment
        builder.ToTable("JobStatusHistory", tb => tb.HasComment("Complete history of status changes for service jobs with comprehensive audit information"));
    }
}