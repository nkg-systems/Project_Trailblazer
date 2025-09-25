using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Common;

namespace FieldOpsOptimizer.Domain.Services;

/// <summary>
/// Domain service for validating job status transitions and enforcing business rules
/// </summary>
public class JobStatusTransitionService
{
    /// <summary>
    /// Valid status transitions mapping
    /// </summary>
    private static readonly Dictionary<JobStatus, JobStatus[]> ValidTransitions = new()
    {
        {
            JobStatus.Scheduled, new[]
            {
                JobStatus.InProgress,
                JobStatus.OnHold,
                JobStatus.Cancelled
            }
        },
        {
            JobStatus.InProgress, new[]
            {
                JobStatus.Completed,
                JobStatus.OnHold,
                JobStatus.Cancelled
            }
        },
        {
            JobStatus.OnHold, new[]
            {
                JobStatus.InProgress,
                JobStatus.Scheduled,
                JobStatus.Cancelled
            }
        },
        // Completed and Cancelled are terminal states - no transitions allowed
        {
            JobStatus.Completed, Array.Empty<JobStatus>()
        },
        {
            JobStatus.Cancelled, Array.Empty<JobStatus>()
        }
    };

    /// <summary>
    /// Status transitions that require special permissions
    /// </summary>
    private static readonly HashSet<(JobStatus From, JobStatus To)> RestrictedTransitions = new()
    {
        (JobStatus.Completed, JobStatus.InProgress), // Reopening completed job
        (JobStatus.Cancelled, JobStatus.Scheduled),  // Uncancelling job
        (JobStatus.InProgress, JobStatus.Scheduled)  // Moving back from in-progress to scheduled
    };

    /// <summary>
    /// Status transitions that should trigger automatic notifications
    /// </summary>
    private static readonly HashSet<JobStatus> NotificationTriggeringStatuses = new()
    {
        JobStatus.InProgress,
        JobStatus.Completed,
        JobStatus.Cancelled,
        JobStatus.OnHold
    };

    /// <summary>
    /// Validates if a status transition is allowed
    /// </summary>
    /// <param name="fromStatus">Current status</param>
    /// <param name="toStatus">Desired new status</param>
    /// <param name="userRole">Role of the user attempting the transition</param>
    /// <param name="hasSpecialPermissions">Whether user has special permissions for restricted transitions</param>
    /// <returns>Validation result</returns>
    public StatusTransitionResult ValidateTransition(
        JobStatus fromStatus,
        JobStatus toStatus,
        string? userRole = null,
        bool hasSpecialPermissions = false)
    {
        // No-op if status is not changing
        if (fromStatus == toStatus)
        {
            return StatusTransitionResult.Success();
        }

        // Check if transition is valid according to business rules
        if (!ValidTransitions.ContainsKey(fromStatus))
        {
            return StatusTransitionResult.Failure(
                $"Invalid source status: {fromStatus}",
                "INVALID_SOURCE_STATUS");
        }

        var allowedTransitions = ValidTransitions[fromStatus];
        if (!allowedTransitions.Contains(toStatus))
        {
            return StatusTransitionResult.Failure(
                $"Invalid transition from {fromStatus} to {toStatus}",
                "INVALID_TRANSITION");
        }

        // Check if transition requires special permissions
        if (RestrictedTransitions.Contains((fromStatus, toStatus)) && !hasSpecialPermissions)
        {
            return StatusTransitionResult.Failure(
                $"Transition from {fromStatus} to {toStatus} requires special permissions",
                "INSUFFICIENT_PERMISSIONS");
        }

        // Additional business rule validations
        var businessRuleValidation = ValidateBusinessRules(fromStatus, toStatus, userRole);
        if (!businessRuleValidation.IsValid)
        {
            return businessRuleValidation;
        }

        return StatusTransitionResult.Success(
            shouldTriggerNotifications: NotificationTriggeringStatuses.Contains(toStatus));
    }

    /// <summary>
    /// Validates business rules for specific status transitions
    /// </summary>
    /// <param name="fromStatus">Current status</param>
    /// <param name="toStatus">Desired new status</param>
    /// <param name="userRole">Role of the user attempting the transition</param>
    /// <returns>Validation result</returns>
    private static StatusTransitionResult ValidateBusinessRules(
        JobStatus fromStatus,
        JobStatus toStatus,
        string? userRole)
    {
        var warnings = new List<string>();

        // Business Rule: Only technicians or supervisors can start jobs
        if (toStatus == JobStatus.InProgress)
        {
            if (!string.IsNullOrWhiteSpace(userRole) && 
                !IsAuthorizedToStartJob(userRole))
            {
                return StatusTransitionResult.Failure(
                    "Only technicians or supervisors can start jobs",
                    "UNAUTHORIZED_JOB_START");
            }
        }

        // Business Rule: Warn when moving from InProgress back to Scheduled
        if (fromStatus == JobStatus.InProgress && toStatus == JobStatus.Scheduled)
        {
            warnings.Add("Moving job from InProgress back to Scheduled - consider using OnHold instead");
        }

        // Business Rule: Completing a job that was never started
        if (fromStatus == JobStatus.Scheduled && toStatus == JobStatus.Completed)
        {
            warnings.Add("Job is being completed without being started - this may affect time tracking");
        }

        // Business Rule: Jobs on hold for extended periods should be reviewed
        if (toStatus == JobStatus.OnHold)
        {
            warnings.Add("Job placed on hold - remember to review and reschedule");
        }

        return StatusTransitionResult.Success(warnings: warnings);
    }

    /// <summary>
    /// Gets all valid transitions from a given status
    /// </summary>
    /// <param name="fromStatus">Current status</param>
    /// <param name="hasSpecialPermissions">Whether user has special permissions</param>
    /// <returns>List of valid target statuses</returns>
    public List<JobStatus> GetValidTransitions(JobStatus fromStatus, bool hasSpecialPermissions = false)
    {
        if (!ValidTransitions.ContainsKey(fromStatus))
            return new List<JobStatus>();

        var validTransitions = ValidTransitions[fromStatus].ToList();

        // Filter out restricted transitions if user doesn't have special permissions
        if (!hasSpecialPermissions)
        {
            validTransitions = validTransitions
                .Where(toStatus => !RestrictedTransitions.Contains((fromStatus, toStatus)))
                .ToList();
        }

        return validTransitions;
    }

    /// <summary>
    /// Determines if a status transition should trigger priority escalation
    /// </summary>
    /// <param name="fromStatus">Current status</param>
    /// <param name="toStatus">New status</param>
    /// <param name="currentPriority">Current job priority</param>
    /// <param name="timeInCurrentStatus">How long job has been in current status</param>
    /// <returns>True if priority should be escalated</returns>
    public bool ShouldEscalatePriority(
        JobStatus fromStatus,
        JobStatus toStatus,
        JobPriority currentPriority,
        TimeSpan timeInCurrentStatus)
    {
        // Don't escalate completed or cancelled jobs
        if (toStatus == JobStatus.Completed || toStatus == JobStatus.Cancelled)
            return false;

        // Don't escalate if already at Emergency priority
        if (currentPriority == JobPriority.Emergency)
            return false;

        // Escalate jobs that have been on hold for too long
        if (toStatus == JobStatus.OnHold && timeInCurrentStatus > TimeSpan.FromDays(1))
            return true;

        // Escalate scheduled jobs that haven't started after deadline
        if (fromStatus == JobStatus.Scheduled && 
            toStatus == JobStatus.OnHold && 
            timeInCurrentStatus > TimeSpan.FromHours(4))
            return true;

        return false;
    }

    /// <summary>
    /// Gets recommended actions for a status transition
    /// </summary>
    /// <param name="fromStatus">Current status</param>
    /// <param name="toStatus">New status</param>
    /// <param name="job">The job being transitioned</param>
    /// <returns>List of recommended actions</returns>
    public List<string> GetRecommendedActions(JobStatus fromStatus, JobStatus toStatus, ServiceJob job)
    {
        var recommendations = new List<string>();

        switch (toStatus)
        {
            case JobStatus.InProgress:
                recommendations.Add("Ensure technician has all required tools and parts");
                recommendations.Add("Update customer with estimated arrival time");
                if (job.Priority == JobPriority.Emergency)
                    recommendations.Add("Notify supervisor of emergency job start");
                break;

            case JobStatus.OnHold:
                recommendations.Add("Document reason for hold and expected resolution time");
                recommendations.Add("Notify customer of delay and provide updates");
                recommendations.Add("Consider reassigning to available technician");
                break;

            case JobStatus.Completed:
                recommendations.Add("Capture completion photos and customer signature");
                recommendations.Add("Update inventory for parts used");
                recommendations.Add("Send completion notification to customer");
                recommendations.Add("Schedule follow-up if required");
                break;

            case JobStatus.Cancelled:
                recommendations.Add("Document cancellation reason thoroughly");
                recommendations.Add("Notify customer of cancellation");
                recommendations.Add("Process any applicable refunds");
                if (fromStatus == JobStatus.InProgress)
                    recommendations.Add("Review partial work completed and update billing");
                break;
        }

        return recommendations;
    }

    /// <summary>
    /// Creates a status history entry with full audit information
    /// </summary>
    /// <param name="job">The job being transitioned</param>
    /// <param name="toStatus">New status</param>
    /// <param name="changedByUserId">User making the change</param>
    /// <param name="changedByUserName">Name of user making the change</param>
    /// <param name="changedByUserRole">Role of user making the change</param>
    /// <param name="reason">Reason for the change</param>
    /// <param name="auditInfo">Audit information</param>
    /// <param name="isAutomatic">Whether this was an automatic change</param>
    /// <param name="changeSource">Source of the change</param>
    /// <param name="previousStatusDuration">Duration in previous status</param>
    /// <returns>Job status history entry</returns>
    public JobStatusHistory CreateStatusHistoryEntry(
        ServiceJob job,
        JobStatus toStatus,
        Guid changedByUserId,
        string changedByUserName,
        string? changedByUserRole = null,
        string? reason = null,
        AuditInfo? auditInfo = null,
        bool isAutomatic = false,
        string? changeSource = null,
        TimeSpan? previousStatusDuration = null)
    {
        var historyEntry = new JobStatusHistory(
            job.Id,
            job.JobNumber,
            job.TenantId,
            job.Status, // fromStatus
            toStatus,
            changedByUserId,
            changedByUserName,
            changedByUserRole,
            reason,
            isAutomatic,
            changeSource,
            auditInfo,
            previousStatusDuration);

        // Add business rules that were applied
        var appliedRules = GetAppliedBusinessRules(job.Status, toStatus);
        if (appliedRules.Any())
        {
            historyEntry.RecordAppliedBusinessRules(string.Join("; ", appliedRules));
        }

        return historyEntry;
    }

    /// <summary>
    /// Determines if a user role is authorized to start a job
    /// </summary>
    /// <param name="userRole">User role</param>
    /// <returns>True if authorized</returns>
    private static bool IsAuthorizedToStartJob(string userRole)
    {
        var authorizedRoles = new[] { "Technician", "Senior Technician", "Supervisor", "Manager", "Admin" };
        return authorizedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets business rules that were applied for a specific transition
    /// </summary>
    /// <param name="fromStatus">From status</param>
    /// <param name="toStatus">To status</param>
    /// <returns>List of applied business rules</returns>
    private static List<string> GetAppliedBusinessRules(JobStatus fromStatus, JobStatus toStatus)
    {
        var rules = new List<string>();

        if (toStatus == JobStatus.InProgress)
            rules.Add("Job start authorization verified");

        if (toStatus == JobStatus.Completed)
            rules.Add("Job completion workflow initiated");

        if (toStatus == JobStatus.OnHold)
            rules.Add("Hold status notifications scheduled");

        if (toStatus == JobStatus.Cancelled)
            rules.Add("Cancellation workflow initiated");

        return rules;
    }
}

/// <summary>
/// Result of a status transition validation
/// </summary>
public class StatusTransitionResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ErrorCode { get; private set; }
    public List<string> Warnings { get; private set; } = new();
    public bool ShouldTriggerNotifications { get; private set; }
    public List<string> RecommendedActions { get; private set; } = new();

    private StatusTransitionResult(bool isValid)
    {
        IsValid = isValid;
    }

    public static StatusTransitionResult Success(
        bool shouldTriggerNotifications = false,
        List<string>? warnings = null,
        List<string>? recommendedActions = null)
    {
        return new StatusTransitionResult(true)
        {
            ShouldTriggerNotifications = shouldTriggerNotifications,
            Warnings = warnings ?? new List<string>(),
            RecommendedActions = recommendedActions ?? new List<string>()
        };
    }

    public static StatusTransitionResult Failure(
        string errorMessage,
        string errorCode,
        List<string>? warnings = null)
    {
        return new StatusTransitionResult(false)
        {
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            Warnings = warnings ?? new List<string>()
        };
    }
}