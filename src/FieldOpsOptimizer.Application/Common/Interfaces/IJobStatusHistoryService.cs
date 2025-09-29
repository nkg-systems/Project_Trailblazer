using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Application.Common.Interfaces;

public interface IJobStatusHistoryService
{
    // Queries
    Task<IReadOnlyList<JobStatusHistory>> GetByServiceJobAsync(Guid serviceJobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobStatusHistory>> FilterAsync(
        Guid? serviceJobId = null,
        string? jobNumber = null,
        JobStatus? fromStatus = null,
        JobStatus? toStatus = null,
        Guid? changedByUserId = null,
        string? changedByUserName = null,
        DateTime? changedFrom = null,
        DateTime? changedTo = null,
        bool? isAutomaticChange = null,
        string? changeSource = null,
        int? minDurationMinutes = null,
        int? maxDurationMinutes = null,
        CancellationToken cancellationToken = default);

    // Commands
    Task<JobStatusHistory> RecordAsync(
        Guid serviceJobId,
        string jobNumber,
        JobStatus fromStatus,
        JobStatus toStatus,
        Guid changedByUserId,
        string changedByUserName,
        string? changedByUserRole = null,
        string? reason = null,
        bool isAutomatic = false,
        string? changeSource = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? sessionId = null,
        TimeSpan? previousStatusDuration = null,
        bool triggeredNotifications = false,
        string? validationWarnings = null,
        string? appliedBusinessRules = null,
        CancellationToken cancellationToken = default);

    // Analytics
    Task<(int TotalTransitions, int Automatic, int Manual)> GetStatsAsync(
        Guid serviceJobId,
        CancellationToken cancellationToken = default);
}