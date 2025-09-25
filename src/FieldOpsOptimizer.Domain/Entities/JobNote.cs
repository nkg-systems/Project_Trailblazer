using FieldOpsOptimizer.Domain.Common;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Domain.Entities;

/// <summary>
/// Represents a note attached to a service job with full audit trail and security features
/// </summary>
public class JobNote : BaseEntity
{
    public const int MaxContentLength = 4000;
    public const int MaxAuthorNameLength = 200;
    public const int MaxIpAddressLength = 45; // IPv6 max length
    public const int MaxUserAgentLength = 500;

    private string _content = string.Empty;
    private string _authorName = string.Empty;

    /// <summary>
    /// The content of the note (encrypted in database)
    /// </summary>
    public string Content 
    { 
        get => _content;
        private set => _content = ValidateContent(value);
    }

    /// <summary>
    /// Type of note determining visibility and purpose
    /// </summary>
    public JobNoteType Type { get; private set; }

    /// <summary>
    /// Whether this note should be visible to customers
    /// </summary>
    public bool IsCustomerVisible { get; private set; }

    /// <summary>
    /// Whether this note contains sensitive information
    /// </summary>
    public bool IsSensitive { get; private set; }

    /// <summary>
    /// ID of the user who created the note
    /// </summary>
    public Guid AuthorUserId { get; private set; }

    /// <summary>
    /// Name of the user who created the note (cached for performance)
    /// </summary>
    public string AuthorName 
    { 
        get => _authorName;
        private set => _authorName = ValidateAuthorName(value);
    }

    /// <summary>
    /// Role/position of the note author at time of creation
    /// </summary>
    public string? AuthorRole { get; private set; }

    /// <summary>
    /// ID of the service job this note belongs to
    /// </summary>
    public Guid ServiceJobId { get; private set; }

    /// <summary>
    /// Tenant ID for multi-tenant isolation
    /// </summary>
    public string TenantId { get; private set; } = string.Empty;

    /// <summary>
    /// IP address of the user who created the note (for audit trail)
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// User agent of the client that created the note
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Session ID when the note was created
    /// </summary>
    public string? SessionId { get; private set; }

    /// <summary>
    /// Whether this note has been soft deleted
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// When the note was soft deleted
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// User ID who deleted the note
    /// </summary>
    public Guid? DeletedByUserId { get; private set; }

    /// <summary>
    /// Name of user who deleted the note
    /// </summary>
    public string? DeletedByUserName { get; private set; }

    /// <summary>
    /// Reason for deletion
    /// </summary>
    public string? DeletionReason { get; private set; }

    /// <summary>
    /// Concurrency token for optimistic locking
    /// </summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Navigation properties
    /// <summary>
    /// The service job this note belongs to
    /// </summary>
    public ServiceJob ServiceJob { get; private set; } = null!;

    private JobNote() { } // For EF Core

    /// <summary>
    /// Creates a new job note
    /// </summary>
    /// <param name="content">The note content</param>
    /// <param name="type">Type of note</param>
    /// <param name="serviceJobId">ID of the service job</param>
    /// <param name="tenantId">Tenant ID for isolation</param>
    /// <param name="authorUserId">ID of the user creating the note</param>
    /// <param name="authorName">Name of the user creating the note</param>
    /// <param name="authorRole">Role of the user creating the note</param>
    /// <param name="isCustomerVisible">Whether customers should see this note</param>
    /// <param name="isSensitive">Whether this note contains sensitive information</param>
    /// <param name="auditInfo">Audit information (IP, User Agent, Session)</param>
    public JobNote(
        string content,
        JobNoteType type,
        Guid serviceJobId,
        string tenantId,
        Guid authorUserId,
        string authorName,
        string? authorRole = null,
        bool isCustomerVisible = false,
        bool isSensitive = false,
        AuditInfo? auditInfo = null)
    {
        if (serviceJobId == Guid.Empty)
            throw new ArgumentException("Service job ID cannot be empty", nameof(serviceJobId));
        
        if (authorUserId == Guid.Empty)
            throw new ArgumentException("Author user ID cannot be empty", nameof(authorUserId));
        
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID is required", nameof(tenantId));

        Content = content;
        Type = type;
        ServiceJobId = serviceJobId;
        TenantId = tenantId;
        AuthorUserId = authorUserId;
        AuthorName = authorName;
        AuthorRole = authorRole;
        IsCustomerVisible = isCustomerVisible;
        IsSensitive = isSensitive;

        // Set audit information
        if (auditInfo != null)
        {
            IpAddress = auditInfo.IpAddress?.Length <= MaxIpAddressLength ? auditInfo.IpAddress : auditInfo.IpAddress?[..MaxIpAddressLength];
            UserAgent = auditInfo.UserAgent?.Length <= MaxUserAgentLength ? auditInfo.UserAgent : auditInfo.UserAgent?[..MaxUserAgentLength];
            SessionId = auditInfo.SessionId;
        }

        // Auto-determine customer visibility based on note type
        if (type == JobNoteType.CustomerCommunication)
        {
            IsCustomerVisible = true;
        }
    }

    /// <summary>
    /// Updates the note content with audit trail
    /// </summary>
    /// <param name="newContent">New content for the note</param>
    /// <param name="updatedByUserId">ID of user making the update</param>
    /// <param name="updatedByUserName">Name of user making the update</param>
    /// <param name="auditInfo">Audit information</param>
    public void UpdateContent(string newContent, Guid updatedByUserId, string updatedByUserName, AuditInfo? auditInfo = null)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot update a deleted note");

        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("Updated by user ID cannot be empty", nameof(updatedByUserId));

        Content = newContent;
        UpdateTimestamp();
        
        // Could add content change history here if needed
        // _contentHistory.Add(new NoteContentHistory(...));
    }

    /// <summary>
    /// Changes the visibility of the note to customers
    /// </summary>
    /// <param name="isVisible">Whether the note should be visible to customers</param>
    /// <param name="changedByUserId">User making the change</param>
    public void SetCustomerVisibility(bool isVisible, Guid changedByUserId)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot modify a deleted note");

        if (Type == JobNoteType.Internal && isVisible)
            throw new InvalidOperationException("Internal notes cannot be made customer-visible");

        if (Type == JobNoteType.CustomerCommunication && !isVisible)
            throw new InvalidOperationException("Customer communication notes must remain customer-visible");

        IsCustomerVisible = isVisible;
        UpdateTimestamp();
    }

    /// <summary>
    /// Marks the note as sensitive or removes sensitive marking
    /// </summary>
    /// <param name="isSensitive">Whether the note contains sensitive information</param>
    /// <param name="changedByUserId">User making the change</param>
    public void SetSensitiveFlag(bool isSensitive, Guid changedByUserId)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot modify a deleted note");

        IsSensitive = isSensitive;
        
        // If marking as sensitive, automatically hide from customers unless it's customer communication
        if (isSensitive && Type != JobNoteType.CustomerCommunication)
        {
            IsCustomerVisible = false;
        }
        
        UpdateTimestamp();
    }

    /// <summary>
    /// Soft deletes the note with audit trail
    /// </summary>
    /// <param name="deletedByUserId">ID of user deleting the note</param>
    /// <param name="deletedByUserName">Name of user deleting the note</param>
    /// <param name="reason">Reason for deletion</param>
    public void SoftDelete(Guid deletedByUserId, string deletedByUserName, string? reason = null)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Note is already deleted");

        if (deletedByUserId == Guid.Empty)
            throw new ArgumentException("Deleted by user ID cannot be empty", nameof(deletedByUserId));

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedByUserId = deletedByUserId;
        DeletedByUserName = deletedByUserName;
        DeletionReason = reason;
        
        UpdateTimestamp();
    }

    /// <summary>
    /// Restores a soft-deleted note
    /// </summary>
    /// <param name="restoredByUserId">ID of user restoring the note</param>
    public void Restore(Guid restoredByUserId)
    {
        if (!IsDeleted)
            throw new InvalidOperationException("Note is not deleted");

        IsDeleted = false;
        DeletedAt = null;
        DeletedByUserId = null;
        DeletedByUserName = null;
        DeletionReason = null;
        
        UpdateTimestamp();
    }

    /// <summary>
    /// Determines if the note can be viewed by a user with specific permissions
    /// </summary>
    /// <param name="canViewInternal">Whether the user can view internal notes</param>
    /// <param name="canViewSensitive">Whether the user can view sensitive notes</param>
    /// <param name="isCustomer">Whether the viewer is a customer</param>
    /// <returns>True if the note can be viewed</returns>
    public bool CanBeViewedBy(bool canViewInternal, bool canViewSensitive, bool isCustomer = false)
    {
        if (IsDeleted)
            return false;

        if (isCustomer)
            return IsCustomerVisible && !IsSensitive;

        if (Type == JobNoteType.Internal && !canViewInternal)
            return false;

        if (IsSensitive && !canViewSensitive)
            return false;

        return true;
    }

    /// <summary>
    /// Gets a sanitized version of the note content for logging
    /// </summary>
    /// <returns>Truncated, non-sensitive content for logs</returns>
    public string GetLogSafeContent()
    {
        if (IsSensitive)
            return "[SENSITIVE CONTENT]";

        var content = Content.Length > 100 ? $"{Content[..97]}..." : Content;
        return content.Replace('\n', ' ').Replace('\r', ' ');
    }

    private static string ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Note content cannot be empty", nameof(content));

        if (content.Length > MaxContentLength)
            throw new ArgumentException($"Note content cannot exceed {MaxContentLength} characters", nameof(content));

        // Basic content validation - in production, you might want more sophisticated validation
        content = content.Trim();
        
        return content;
    }

    private static string ValidateAuthorName(string authorName)
    {
        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("Author name cannot be empty", nameof(authorName));

        if (authorName.Length > MaxAuthorNameLength)
            throw new ArgumentException($"Author name cannot exceed {MaxAuthorNameLength} characters", nameof(authorName));

        return authorName.Trim();
    }
}

/// <summary>
/// Audit information for tracking user actions
/// </summary>
public record AuditInfo(
    string? IpAddress = null,
    string? UserAgent = null,
    string? SessionId = null);