using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Application.Common.Interfaces;

public interface IJobNoteService
{
    // Queries
    Task<JobNote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobNote>> GetByServiceJobAsync(Guid serviceJobId, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobNote>> FilterAsync(
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
        CancellationToken cancellationToken = default);

    // Commands
    Task<JobNote> CreateAsync(
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
        CancellationToken cancellationToken = default);

    Task<JobNote> UpdateAsync(
        Guid id,
        string content,
        JobNoteType type,
        bool isCustomerVisible,
        bool isSensitive,
        Guid updatedByUserId,
        string updatedByUserName,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(
        Guid id,
        Guid deletedByUserId,
        string deletedByUserName,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        Guid id,
        Guid restoredByUserId,
        CancellationToken cancellationToken = default);

    // Analytics
    Task<(int Total, int CustomerVisible, int Sensitive, int Deleted)> GetStatsAsync(
        Guid serviceJobId,
        CancellationToken cancellationToken = default);
}