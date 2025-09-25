using FieldOpsOptimizer.Domain.Common;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Domain.Entities;

/// <summary>
/// Tracks the complete history of status changes for service jobs with comprehensive audit information
/// </summary>
public class JobStatusHistory : BaseEntity
{
    public const int MaxReasonLength = 500;
    public const int MaxUserNameLength = 200;
    public const int MaxIpAddressLength = 45; // IPv6 max length
    public const int MaxUserAgentLength = 500;

    private string? _reason;
    private string _changedByUserName = string.Empty;

    /// <summary>
    /// ID of the service job whose status changed
    /// </summary>
    public Guid ServiceJobId { get; private set; }

    /// <summary>
    /// Job number for easier identification in logs and reports
    /// </summary>
    public string JobNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Tenant ID for multi-tenant isolation
    /// </summary>
    public string TenantId { get; private set; } = string.Empty;

    /// <summary>
    /// The status before the change
    /// </summary>
    public JobStatus FromStatus { get; private set; }

    /// <summary>
    /// The status after the change
    /// </summary>
    public JobStatus ToStatus { get; private set; }

    /// <summary>
    /// When the status change occurred (separate from audit timestamps)
    /// </summary>
    public DateTime ChangedAt { get; private set; }

    /// <summary>
    /// ID of the user who made the status change
    /// </summary>
    public Guid ChangedByUserId { get; private set; }

    /// <summary>
    /// Name of the user who made the status change (cached for performance)
    /// </summary>
    public string ChangedByUserName 
    { 
        get => _changedByUserName;
        private set => _changedByUserName = ValidateUserName(value);
    }

    /// <summary>
    /// Role of the user at the time of the change
    /// </summary>
    public string? ChangedByUserRole { get; private set; }

    /// <summary>
    /// Optional reason or comment for the status change
    /// </summary>
    public string? Reason 
    { 
        get => _reason;
        private set => _reason = ValidateReason(value);
    }

    /// <summary>
    /// Whether this was an automatic status change (vs manual)
    /// </summary>
    public bool IsAutomaticChange { get; private set; }

    /// <summary>
    /// Source system or component that triggered the change
    /// </summary>
    public string? ChangeSource { get; private set; }

    /// <summary>
    /// IP address from which the change was made
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// User agent of the client that made the change
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Session ID when the change was made
    /// </summary>
    public string? SessionId { get; private set; }

    /// <summary>
    /// Duration the job was in the previous status (in minutes)
    /// </summary>
    public int? PreviousStatusDurationMinutes { get; private set; }

    /// <summary>
    /// Whether this status change triggered any business rules or notifications
    /// </summary>
    public bool TriggeredNotifications { get; private set; }

    /// <summary>
    /// Any validation warnings that occurred during the status change
    /// </summary>
    public string? ValidationWarnings { get; private set; }

    /// <summary>
    /// Business rules that were applied during this status change
    /// </summary>
    public string? AppliedBusinessRules { get; private set; }

    // Navigation properties
    /// <summary>
    /// The service job this history entry belongs to
    /// </summary>
    public ServiceJob ServiceJob { get; private set; } = null!;

    private JobStatusHistory() { } // For EF Core

    /// <summary>
    /// Creates a new job status history entry
    /// </summary>
    /// <param name="serviceJobId">ID of the service job</param>
    /// <param name="jobNumber">Job number for reference</param>
    /// <param name="tenantId">Tenant ID for isolation</param>
    /// <param name="fromStatus">Previous status</param>
    /// <param name="toStatus">New status</param>
    /// <param name="changedByUserId">ID of user making the change</param>
    /// <param name="changedByUserName">Name of user making the change</param>
    /// <param name="changedByUserRole">Role of user making the change</param>
    /// <param name="reason">Optional reason for the change</param>
    /// <param name="isAutomaticChange">Whether this was an automatic change</param>
    /// <param name="changeSource">Source system or component</param>
    /// <param name="auditInfo">Audit information</param>
    /// <param name="previousStatusDuration">How long the job was in the previous status</param>
    public JobStatusHistory(
        Guid serviceJobId,
        string jobNumber,
        string tenantId,
        JobStatus fromStatus,
        JobStatus toStatus,
        Guid changedByUserId,
        string changedByUserName,
        string? changedByUserRole = null,
        string? reason = null,
        bool isAutomaticChange = false,
        string? changeSource = null,
        AuditInfo? auditInfo = null,
        TimeSpan? previousStatusDuration = null)
    {
        if (serviceJobId == Guid.Empty)
            throw new ArgumentException("Service job ID cannot be empty", nameof(serviceJobId));

        if (changedByUserId == Guid.Empty)
            throw new ArgumentException("Changed by user ID cannot be empty", nameof(changedByUserId));

        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID is required", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(jobNumber))
            throw new ArgumentException("Job number is required", nameof(jobNumber));

        ServiceJobId = serviceJobId;
        JobNumber = jobNumber.Trim();
        TenantId = tenantId.Trim();
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedAt = DateTime.UtcNow;
        ChangedByUserId = changedByUserId;
        ChangedByUserName = changedByUserName;
        ChangedByUserRole = changedByUserRole?.Trim();
        Reason = reason;
        IsAutomaticChange = isAutomaticChange;
        ChangeSource = changeSource?.Trim();

        // Set audit information
        if (auditInfo != null)
        {
            IpAddress = auditInfo.IpAddress?.Length <= MaxIpAddressLength ? auditInfo.IpAddress : auditInfo.IpAddress?[..MaxIpAddressLength];
            UserAgent = auditInfo.UserAgent?.Length <= MaxUserAgentLength ? auditInfo.UserAgent : auditInfo.UserAgent?[..MaxUserAgentLength];
            SessionId = auditInfo.SessionId;
        }

        // Calculate previous status duration if provided
        if (previousStatusDuration.HasValue)
        {
            PreviousStatusDurationMinutes = (int)previousStatusDuration.Value.TotalMinutes;
        }
    }

    /// <summary>
    /// Marks that this status change triggered notifications
    /// </summary>
    /// <param name="notificationTypes">Types of notifications that were sent</param>
    public void SetNotificationsSent(string? notificationTypes = null)
    {
        TriggeredNotifications = true;
        if (!string.IsNullOrWhiteSpace(notificationTypes))
        {
            AppliedBusinessRules = string.IsNullOrWhiteSpace(AppliedBusinessRules) 
                ? $"Notifications: {notificationTypes}"
                : $"{AppliedBusinessRules}; Notifications: {notificationTypes}";
        }
    }

    /// <summary>
    /// Adds validation warnings that occurred during the status change
    /// </summary>
    /// <param name="warnings">Validation warnings</param>
    public void AddValidationWarnings(string warnings)
    {
        if (string.IsNullOrWhiteSpace(warnings))
            return;

        ValidationWarnings = string.IsNullOrWhiteSpace(ValidationWarnings)
            ? warnings
            : $"{ValidationWarnings}; {warnings}";
    }

    /// <summary>
    /// Records business rules that were applied during the status change
    /// </summary>
    /// <param name="rules">Business rules that were applied</param>
    public void RecordAppliedBusinessRules(string rules)
    {
        if (string.IsNullOrWhiteSpace(rules))
            return;

        AppliedBusinessRules = string.IsNullOrWhiteSpace(AppliedBusinessRules)
            ? rules
            : $"{AppliedBusinessRules}; {rules}";
    }

    /// <summary>
    /// Determines if this status change represents a significant milestone
    /// </summary>
    /// <returns>True if this is a milestone status change</returns>
    public bool IsSignificantMilestone()
    {
        return ToStatus switch
        {
            JobStatus.InProgress => true, // Job started
            JobStatus.Completed => true, // Job completed
            JobStatus.Cancelled => true, // Job cancelled
            JobStatus.OnHold => FromStatus == JobStatus.InProgress, // Job paused during work
            _ => false
        };
    }

    /// <summary>
    /// Gets the duration the job was in the previous status as a TimeSpan
    /// </summary>
    /// <returns>Duration or null if not available</returns>
    public TimeSpan? GetPreviousStatusDuration()
    {
        return PreviousStatusDurationMinutes.HasValue 
            ? TimeSpan.FromMinutes(PreviousStatusDurationMinutes.Value)
            : null;
    }

    /// <summary>
    /// Gets a formatted description of the status change for display
    /// </summary>
    /// <returns>Human-readable description of the change</returns>
    public string GetChangeDescription()
    {
        var description = $"Status changed from {FromStatus} to {ToStatus}";
        
        if (!string.IsNullOrWhiteSpace(Reason))
            description += $" - {Reason}";

        if (IsAutomaticChange)
            description += " (Automatic)";

        return description;
    }

    /// <summary>
    /// Gets a log-safe version of the change for audit logs
    /// </summary>
    /// <returns>Log-safe string representation</returns>
    public string GetLogSafeDescription()
    {
        return $"Job {JobNumber}: {FromStatus} → {ToStatus} by {ChangedByUserName}" +
               (IsAutomaticChange ? " (Auto)" : "") +
               (!string.IsNullOrWhiteSpace(ChangeSource) ? $" via {ChangeSource}" : "");
    }

    /// <summary>
    /// Validates if this represents a valid status transition
    /// </summary>
    /// <param name="validTransitions">Dictionary of valid status transitions</param>
    /// <returns>True if the transition is valid</returns>
    public bool IsValidTransition(Dictionary<JobStatus, JobStatus[]> validTransitions)
    {
        if (!validTransitions.ContainsKey(FromStatus))
            return false;

        return validTransitions[FromStatus].Contains(ToStatus);
    }

    private static string? ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        reason = reason.Trim();
        if (reason.Length > MaxReasonLength)
            reason = reason[..MaxReasonLength];

        return reason;
    }

    private static string ValidateUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("User name cannot be empty", nameof(userName));

        if (userName.Length > MaxUserNameLength)
            throw new ArgumentException($"User name cannot exceed {MaxUserNameLength} characters", nameof(userName));

        return userName.Trim();
    }
}