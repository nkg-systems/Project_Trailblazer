using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FieldOpsOptimizer.Infrastructure.Services;

public class JobNoteService : IJobNoteService
{
    private readonly ApplicationDbContext _db;
    private readonly Repository<JobNote> _repo;
    private readonly ITenantService _tenantService;

    public JobNoteService(ApplicationDbContext db, ITenantService tenantService)
    {
        _db = db;
        _repo = new Repository<JobNote>(db);
        _tenantService = tenantService;
    }

    public async Task<JobNote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        return await _db.JobNotes
            .Where(n => n.Id == id && n.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobNote>> GetByServiceJobAsync(Guid serviceJobId, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        var query = _db.JobNotes
            .Where(n => n.ServiceJobId == serviceJobId && n.TenantId == tenantId);

        if (!includeDeleted)
            query = query.Where(n => !n.IsDeleted);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobNote>> FilterAsync(
        Guid? serviceJobId = null,
        JobNoteType? type = null,
        bool? isCustomerVisible = null,
        bool? isSensitive = null,
        Guid? authorUserId = null,
        string? authorName = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        bool includeDeleted = false,
        string? contentSearch = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        var query = _db.JobNotes.AsQueryable().Where(n => n.TenantId == tenantId);

        if (!includeDeleted)
            query = query.Where(n => !n.IsDeleted);
        if (serviceJobId.HasValue)
            query = query.Where(n => n.ServiceJobId == serviceJobId.Value);
        if (type.HasValue)
            query = query.Where(n => n.Type == type.Value);
        if (isCustomerVisible.HasValue)
            query = query.Where(n => n.IsCustomerVisible == isCustomerVisible.Value);
        if (isSensitive.HasValue)
            query = query.Where(n => n.IsSensitive == isSensitive.Value);
        if (authorUserId.HasValue)
            query = query.Where(n => n.AuthorUserId == authorUserId.Value);
        if (!string.IsNullOrWhiteSpace(authorName))
            query = query.Where(n => n.AuthorName == authorName);
        if (createdFrom.HasValue)
            query = query.Where(n => n.CreatedAt >= createdFrom.Value);
        if (createdTo.HasValue)
            query = query.Where(n => n.CreatedAt <= createdTo.Value);
        if (!string.IsNullOrWhiteSpace(contentSearch))
            query = query.Where(n => EF.Functions.ILike(n.Content, $"%{contentSearch}%"));

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobNote> CreateAsync(
        string content,
        JobNoteType type,
        Guid serviceJobId,
        Guid authorUserId,
        string authorName,
        string? authorRole = null,
        bool isCustomerVisible = false,
        bool isSensitive = false,
        string? ipAddress = null,
        string? userAgent = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("Tenant not resolved");

        var entity = new JobNote(
            content,
            type,
            serviceJobId,
            tenantId,
            authorUserId,
            authorName,
            authorRole,
            isCustomerVisible,
            isSensitive,
            new AuditInfo(ipAddress, userAgent, sessionId));

        _repo.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<JobNote> UpdateAsync(
        Guid id,
        string content,
        JobNoteType type,
        bool isCustomerVisible,
        bool isSensitive,
        Guid updatedByUserId,
        string updatedByUserName,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        var entity = await _db.JobNotes.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Note not found");

        entity.UpdateContent(content, updatedByUserId, updatedByUserName);
        if (entity.Type != JobNoteType.CustomerCommunication)
        {
            entity.SetCustomerVisibility(isCustomerVisible, updatedByUserId);
        }
        entity.SetSensitiveFlag(isSensitive, updatedByUserId);

        _repo.Update(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task SoftDeleteAsync(
        Guid id,
        Guid deletedByUserId,
        string deletedByUserName,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        var entity = await _db.JobNotes.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Note not found");

        entity.SoftDelete(deletedByUserId, deletedByUserName, reason);
        _repo.Update(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(
        Guid id,
        Guid restoredByUserId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        var entity = await _db.JobNotes.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Note not found");

        entity.Restore(restoredByUserId);
        _repo.Update(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(int Total, int CustomerVisible, int Sensitive, int Deleted)> GetStatsAsync(Guid serviceJobId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        var baseQuery = _db.JobNotes.Where(n => n.ServiceJobId == serviceJobId && n.TenantId == tenantId);

        var total = await baseQuery.CountAsync(cancellationToken);
        var customerVisible = await baseQuery.Where(n => n.IsCustomerVisible && !n.IsSensitive && !n.IsDeleted).CountAsync(cancellationToken);
        var sensitive = await baseQuery.Where(n => n.IsSensitive && !n.IsDeleted).CountAsync(cancellationToken);
        var deleted = await baseQuery.Where(n => n.IsDeleted).CountAsync(cancellationToken);

        return (total, customerVisible, sensitive, deleted);
    }
}