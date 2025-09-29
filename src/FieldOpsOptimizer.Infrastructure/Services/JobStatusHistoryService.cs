using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FieldOpsOptimizer.Infrastructure.Services;

public class JobStatusHistoryService : IJobStatusHistoryService
{
    private readonly ApplicationDbContext _db;
    private readonly Repository<JobStatusHistory> _repo;
    private readonly ITenantService _tenantService;

    public JobStatusHistoryService(ApplicationDbContext db, ITenantService tenantService)
    {
        _db = db;
        _repo = new Repository<JobStatusHistory>(db);
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<JobStatusHistory>> GetByServiceJobAsync(Guid serviceJobId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        return await _db.JobStatusHistory
            .Where(h => h.ServiceJobId == serviceJobId && h.TenantId == tenantId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobStatusHistory>> FilterAsync(
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
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        var query = _db.JobStatusHistory.AsQueryable().Where(h => h.TenantId == tenantId);

        if (serviceJobId.HasValue)
            query = query.Where(h => h.ServiceJobId == serviceJobId.Value);
        if (!string.IsNullOrWhiteSpace(jobNumber))
            query = query.Where(h => h.JobNumber == jobNumber);
        if (fromStatus.HasValue)
            query = query.Where(h => h.FromStatus == fromStatus.Value);
        if (toStatus.HasValue)
            query = query.Where(h => h.ToStatus == toStatus.Value);
        if (changedByUserId.HasValue)
            query = query.Where(h => h.ChangedByUserId == changedByUserId.Value);
        if (!string.IsNullOrWhiteSpace(changedByUserName))
            query = query.Where(h => h.ChangedByUserName == changedByUserName);
        if (changedFrom.HasValue)
            query = query.Where(h => h.ChangedAt >= changedFrom.Value);
        if (changedTo.HasValue)
            query = query.Where(h => h.ChangedAt <= changedTo.Value);
        if (isAutomaticChange.HasValue)
            query = query.Where(h => h.IsAutomaticChange == isAutomaticChange.Value);
        if (!string.IsNullOrWhiteSpace(changeSource))
            query = query.Where(h => h.ChangeSource == changeSource);
        if (minDurationMinutes.HasValue)
            query = query.Where(h => h.PreviousStatusDurationMinutes >= minDurationMinutes.Value);
        if (maxDurationMinutes.HasValue)
            query = query.Where(h => h.PreviousStatusDurationMinutes <= maxDurationMinutes.Value);

        return await query
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobStatusHistory> RecordAsync(
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
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("Tenant not resolved");

        var entity = new JobStatusHistory(
            serviceJobId,
            jobNumber,
            tenantId,
            fromStatus,
            toStatus,
            changedByUserId,
            changedByUserName,
            changedByUserRole,
            reason,
            isAutomatic,
            changeSource,
            new AuditInfo(ipAddress, userAgent, sessionId),
            previousStatusDuration);

        if (triggeredNotifications)
            entity.SetNotificationsSent();
        if (!string.IsNullOrWhiteSpace(validationWarnings))
            entity.AddValidationWarnings(validationWarnings);
        if (!string.IsNullOrWhiteSpace(appliedBusinessRules))
            entity.RecordAppliedBusinessRules(appliedBusinessRules);

        _repo.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<(int TotalTransitions, int Automatic, int Manual)> GetStatsAsync(Guid serviceJobId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        var baseQuery = _db.JobStatusHistory.Where(h => h.ServiceJobId == serviceJobId && h.TenantId == tenantId);

        var total = await baseQuery.CountAsync(cancellationToken);
        var automatic = await baseQuery.Where(h => h.IsAutomaticChange).CountAsync(cancellationToken);
        var manual = total - automatic;
        return (total, automatic, manual);
    }
}