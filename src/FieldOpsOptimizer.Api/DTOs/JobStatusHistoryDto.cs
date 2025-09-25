using System.ComponentModel.DataAnnotations;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Api.DTOs;

/// <summary>
/// DTO for JobStatusHistory response data
/// </summary>
public class JobStatusHistoryDto
{
    public Guid Id { get; set; }
    public Guid ServiceJobId { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public JobStatus FromStatus { get; set; }
    public JobStatus ToStatus { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid ChangedByUserId { get; set; }
    public string ChangedByUserName { get; set; } = string.Empty;
    public string? ChangedByUserRole { get; set; }
    public string? Reason { get; set; }
    public bool IsAutomaticChange { get; set; }
    public string? ChangeSource { get; set; }
    public int? PreviousStatusDurationMinutes { get; set; }
    public bool TriggeredNotifications { get; set; }
    public string? ValidationWarnings { get; set; }
    public string? AppliedBusinessRules { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;

    // Audit trail (only visible to authorized users)
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
}

/// <summary>
/// Summary DTO for JobStatusHistory with key information only
/// </summary>
public class JobStatusHistorySummaryDto
{
    public Guid Id { get; set; }
    public JobStatus FromStatus { get; set; }
    public JobStatus ToStatus { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedByUserName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public bool IsAutomaticChange { get; set; }
    public TimeSpan? PreviousStatusDuration { get; set; }
}

/// <summary>
/// DTO for filtering job status history with comprehensive options
/// </summary>
public class JobStatusHistoryFilter
{
    public Guid? ServiceJobId { get; set; }
    public string? JobNumber { get; set; }
    public JobStatus? FromStatus { get; set; }
    public JobStatus? ToStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public DateTime? ChangedFrom { get; set; }
    public DateTime? ChangedTo { get; set; }
    public bool? IsAutomaticChange { get; set; }
    public string? ChangeSource { get; set; }
    public bool? TriggeredNotifications { get; set; }
    public string? ReasonSearch { get; set; }
    public int? MinDurationMinutes { get; set; }
    public int? MaxDurationMinutes { get; set; }
}

/// <summary>
/// DTO for creating status transition entries (typically done automatically)
/// </summary>
public class CreateJobStatusHistoryDto
{
    [Required(ErrorMessage = "Service Job ID is required")]
    public Guid ServiceJobId { get; set; }

    [Required(ErrorMessage = "Job Number is required")]
    [StringLength(100, ErrorMessage = "Job Number cannot exceed 100 characters")]
    public string JobNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "From Status is required")]
    public JobStatus FromStatus { get; set; }

    [Required(ErrorMessage = "To Status is required")]
    public JobStatus ToStatus { get; set; }

    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string? Reason { get; set; }

    public bool IsAutomaticChange { get; set; } = false;

    [StringLength(100, ErrorMessage = "Change Source cannot exceed 100 characters")]
    public string? ChangeSource { get; set; }

    public int? PreviousStatusDurationMinutes { get; set; }
    public bool TriggeredNotifications { get; set; } = false;

    [StringLength(1000, ErrorMessage = "Validation Warnings cannot exceed 1000 characters")]
    public string? ValidationWarnings { get; set; }

    [StringLength(1000, ErrorMessage = "Applied Business Rules cannot exceed 1000 characters")]
    public string? AppliedBusinessRules { get; set; }
}

/// <summary>
/// DTO for job status statistics and analytics
/// </summary>
public class JobStatusStatsDto
{
    public int TotalTransitions { get; set; }
    public int AutomaticTransitions { get; set; }
    public int ManualTransitions { get; set; }
    public Dictionary<JobStatus, int> TransitionsFromStatus { get; set; } = new();
    public Dictionary<JobStatus, int> TransitionsToStatus { get; set; } = new();
    public Dictionary<string, int> TransitionsByUser { get; set; } = new();
    public Dictionary<string, int> TransitionsBySource { get; set; } = new();
    public double AverageDurationInStatusMinutes { get; set; }
    public Dictionary<JobStatus, double> AverageDurationByStatus { get; set; } = new();
    public DateTime? OldestTransition { get; set; }
    public DateTime? MostRecentTransition { get; set; }
}

/// <summary>
/// DTO for status transition timeline visualization
/// </summary>
public class StatusTransitionTimelineDto
{
    public Guid ServiceJobId { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public List<StatusTransitionEventDto> Events { get; set; } = new();
    public TimeSpan TotalJobDuration { get; set; }
    public int TotalTransitions { get; set; }
    public DateTime JobCreatedAt { get; set; }
    public DateTime? JobCompletedAt { get; set; }
}

/// <summary>
/// DTO for individual status transition events in timeline
/// </summary>
public class StatusTransitionEventDto
{
    public JobStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public string ChangedByUserName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public bool IsAutomaticChange { get; set; }
    public bool IsCurrentStatus { get; set; }
}

/// <summary>
/// DTO for performance analytics on status transitions
/// </summary>
public class StatusTransitionPerformanceDto
{
    public JobStatus Status { get; set; }
    public int TotalJobs { get; set; }
    public double AverageDurationHours { get; set; }
    public double MedianDurationHours { get; set; }
    public double MinDurationHours { get; set; }
    public double MaxDurationHours { get; set; }
    public double StandardDeviation { get; set; }
    public int JobsExceedingSLA { get; set; }
    public double SLAComplianceRate { get; set; }
}

/// <summary>
/// DTO for bulk status transition operations
/// </summary>
public class BulkStatusTransitionDto
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one job is required")]
    [MaxLength(100, ErrorMessage = "Cannot update more than 100 jobs at once")]
    public List<Guid> ServiceJobIds { get; set; } = new();

    [Required(ErrorMessage = "Target status is required")]
    public JobStatus ToStatus { get; set; }

    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string? Reason { get; set; }

    public bool IsAutomaticChange { get; set; } = false;

    [StringLength(100, ErrorMessage = "Change Source cannot exceed 100 characters")]
    public string? ChangeSource { get; set; }
}

/// <summary>
/// DTO for exporting status history data
/// </summary>
public class JobStatusHistoryExportDto
{
    public List<JobStatusHistoryDto> StatusHistory { get; set; } = new();
    public DateTime ExportedAt { get; set; }
    public string ExportedBy { get; set; } = string.Empty;
    public JobStatusHistoryFilter? AppliedFilter { get; set; }
    public int TotalRecords { get; set; }
}

/// <summary>
/// DTO for status transition validation before execution
/// </summary>
public class ValidateStatusTransitionDto
{
    [Required(ErrorMessage = "Service Job ID is required")]
    public Guid ServiceJobId { get; set; }

    [Required(ErrorMessage = "Current Status is required")]
    public JobStatus FromStatus { get; set; }

    [Required(ErrorMessage = "Target Status is required")]
    public JobStatus ToStatus { get; set; }
}

/// <summary>
/// DTO for status transition validation results
/// </summary>
public class StatusTransitionValidationResultDto
{
    public bool IsValid { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> RequiredPermissions { get; set; } = new();
    public List<string> BusinessRulesToApply { get; set; } = new();
    public List<string> NotificationsToTrigger { get; set; } = new();
    public string? RecommendedAction { get; set; }
}