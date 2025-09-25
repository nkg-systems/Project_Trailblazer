using System.ComponentModel.DataAnnotations;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Api.DTOs;

/// <summary>
/// DTO for JobNote response data
/// </summary>
public class JobNoteDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public JobNoteType Type { get; set; }
    public bool IsCustomerVisible { get; set; }
    public bool IsSensitive { get; set; }
    public Guid ServiceJobId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorRole { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;

    // Audit trail (only visible to authorized users)
    public string? IpAddress { get; set; }
    public string? SessionId { get; set; }
    
    // Only include if user has appropriate permissions
    public bool? IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserName { get; set; }
    public string? DeletionReason { get; set; }
}

/// <summary>
/// DTO for creating a new job note with validation
/// </summary>
public class CreateJobNoteDto
{
    [Required(ErrorMessage = "Note content is required")]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "Note content must be between 1 and 4000 characters")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "Note type is required")]
    public JobNoteType Type { get; set; }

    [Required(ErrorMessage = "Service Job ID is required")]
    public Guid ServiceJobId { get; set; }

    public bool IsCustomerVisible { get; set; } = false;
    public bool IsSensitive { get; set; } = false;
}

/// <summary>
/// DTO for updating an existing job note
/// </summary>
public class UpdateJobNoteDto
{
    [Required(ErrorMessage = "Note content is required")]
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "Note content must be between 1 and 4000 characters")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "Note type is required")]
    public JobNoteType Type { get; set; }

    public bool IsCustomerVisible { get; set; }
    public bool IsSensitive { get; set; }
}

/// <summary>
/// DTO for soft deleting a job note
/// </summary>
public class DeleteJobNoteDto
{
    [StringLength(500, ErrorMessage = "Deletion reason cannot exceed 500 characters")]
    public string? DeletionReason { get; set; }
}

/// <summary>
/// DTO for filtering job notes with security and tenant isolation
/// </summary>
public class JobNoteFilter
{
    public Guid? ServiceJobId { get; set; }
    public JobNoteType? Type { get; set; }
    public bool? IsCustomerVisible { get; set; }
    public bool? IsSensitive { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public bool? IncludeDeleted { get; set; } = false; // Only for authorized users
    public string? ContentSearch { get; set; } // For content-based searching
}

/// <summary>
/// Customer-safe DTO that excludes sensitive information
/// </summary>
public class CustomerJobNoteDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public JobNoteType Type { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    // Customer notes should only show public, non-sensitive information
    // No audit trail, no sensitive flags, no deletion info
}

/// <summary>
/// DTO for bulk operations on job notes
/// </summary>
public class BulkCreateJobNotesDto
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one note is required")]
    [MaxLength(50, ErrorMessage = "Cannot create more than 50 notes at once")]
    public List<CreateJobNoteDto> Notes { get; set; } = new();
}

/// <summary>
/// DTO for job note statistics and analytics
/// </summary>
public class JobNoteStatsDto
{
    public int TotalNotes { get; set; }
    public int CustomerVisibleNotes { get; set; }
    public int SensitiveNotes { get; set; }
    public int DeletedNotes { get; set; }
    public Dictionary<JobNoteType, int> NotesByType { get; set; } = new();
    public Dictionary<string, int> NotesByAuthor { get; set; } = new();
    public DateTime? LastNoteCreated { get; set; }
    public DateTime? OldestNote { get; set; }
}

/// <summary>
/// DTO for job note export functionality
/// </summary>
public class JobNoteExportDto
{
    public string JobNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<JobNoteDto> Notes { get; set; } = new();
    public DateTime ExportedAt { get; set; }
    public string ExportedBy { get; set; } = string.Empty;
}

/// <summary>
/// DTO for job note audit trail information
/// </summary>
public class JobNoteAuditDto
{
    public Guid NoteId { get; set; }
    public string Action { get; set; } = string.Empty; // Created, Updated, Deleted, Viewed
    public string UserName { get; set; } = string.Empty;
    public string? UserRole { get; set; }
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public string? Details { get; set; } // JSON with change details
}