using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Api.DTOs;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using AutoMapper;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// API controller for job notes management operations
/// </summary>
[ApiController]
[Route("api/service-jobs/{serviceJobId}/notes")]
[Route("api/job-notes")]
[Authorize]
[Produces("application/json")]
public class JobNotesController : ControllerBase
{
    private readonly IJobNoteService _jobNoteService;
    private readonly IMapper _mapper;
    private readonly ILogger<JobNotesController> _logger;

    public JobNotesController(
        IJobNoteService jobNoteService,
        IMapper mapper,
        ILogger<JobNotesController> logger)
    {
        _jobNoteService = jobNoteService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Gets all notes for a specific service job
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<JobNoteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobNoteDto>>> GetJobNotes(
        [FromRoute] Guid? serviceJobId = null,
        [FromQuery] JobNoteType? type = null,
        [FromQuery] bool? isCustomerVisible = null,
        [FromQuery] bool? isSensitive = null,
        [FromQuery] Guid? authorUserId = null,
        [FromQuery] string? authorName = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] string? contentSearch = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notes = await _jobNoteService.FilterAsync(
                serviceJobId,
                type,
                isCustomerVisible,
                isSensitive,
                authorUserId,
                authorName,
                createdFrom,
                createdTo,
                includeDeleted,
                contentSearch,
                cancellationToken);

            var dtos = _mapper.Map<List<JobNoteDto>>(notes);
            
            // Filter sensitive information based on user permissions
            if (!HasPermission("ViewSensitiveNotes"))
            {
                dtos = dtos.Where(dto => !dto.IsSensitive).ToList();
                foreach (var dto in dtos)
                {
                    dto.IpAddress = null;
                    dto.SessionId = null;
                }
            }

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job notes for service job {ServiceJobId}", serviceJobId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets a specific job note by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(JobNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobNoteDto>> GetJobNote(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var note = await _jobNoteService.GetByIdAsync(id, cancellationToken);

            if (note == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Job note not found",
                    Detail = $"Job note with ID {id} was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            // Security check for sensitive notes
            if (note.IsSensitive && !HasPermission("ViewSensitiveNotes"))
            {
                return Forbid();
            }

            var dto = _mapper.Map<JobNoteDto>(note);
            
            // Filter audit information based on permissions
            if (!HasPermission("ViewAuditTrail"))
            {
                dto.IpAddress = null;
                dto.SessionId = null;
            }

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job note {NoteId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Creates a new job note
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobNoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobNoteDto>> CreateJobNote(
        [FromBody] CreateJobNoteDto request,
        [FromRoute] Guid? serviceJobId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use serviceJobId from route if provided, otherwise from request body
            var targetServiceJobId = serviceJobId ?? request.ServiceJobId;
            
            var currentUserId = GetCurrentUserId();
            var currentUserName = GetCurrentUserName();
            var currentUserRole = GetCurrentUserRole();
            
            _logger.LogInformation("Creating job note for service job {ServiceJobId} by user {UserId}", 
                targetServiceJobId, currentUserId);

            var note = await _jobNoteService.CreateAsync(
                request.Content,
                request.Type,
                targetServiceJobId,
                currentUserId,
                currentUserName,
                currentUserRole,
                request.IsCustomerVisible,
                request.IsSensitive,
                GetClientIpAddress(),
                GetUserAgent(),
                GetSessionId(),
                cancellationToken);

            var dto = _mapper.Map<JobNoteDto>(note);
            
            _logger.LogInformation("Created job note {NoteId} for service job {ServiceJobId}", 
                note.Id, targetServiceJobId);

            return CreatedAtAction(nameof(GetJobNote), new { id = note.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job note");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Updates an existing job note
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(JobNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobNoteDto>> UpdateJobNote(
        Guid id,
        [FromBody] UpdateJobNoteDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var currentUserName = GetCurrentUserName();
            
            _logger.LogInformation("Updating job note {NoteId} by user {UserId}", id, currentUserId);

            var note = await _jobNoteService.UpdateAsync(
                id,
                request.Content,
                request.Type,
                request.IsCustomerVisible,
                request.IsSensitive,
                currentUserId,
                currentUserName,
                cancellationToken);

            var dto = _mapper.Map<JobNoteDto>(note);
            
            _logger.LogInformation("Updated job note {NoteId}", id);

            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Job note not found",
                Detail = $"Job note with ID {id} was not found"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job note {NoteId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Soft deletes a job note
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteJobNote(
        Guid id,
        [FromBody] DeleteJobNoteDto? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var currentUserName = GetCurrentUserName();
            
            _logger.LogInformation("Soft deleting job note {NoteId} by user {UserId}", id, currentUserId);

            await _jobNoteService.SoftDeleteAsync(
                id,
                currentUserId,
                currentUserName,
                request?.DeletionReason,
                cancellationToken);

            _logger.LogInformation("Soft deleted job note {NoteId}", id);

            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Job note not found",
                Detail = $"Job note with ID {id} was not found"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting job note {NoteId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Restores a soft-deleted job note
    /// </summary>
    [HttpPost("{id}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> RestoreJobNote(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            
            _logger.LogInformation("Restoring job note {NoteId} by user {UserId}", id, currentUserId);

            await _jobNoteService.RestoreAsync(id, currentUserId, cancellationToken);

            _logger.LogInformation("Restored job note {NoteId}", id);

            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Job note not found",
                Detail = $"Job note with ID {id} was not found"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid operation",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring job note {NoteId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets statistics for job notes
    /// </summary>
    [HttpGet("../stats")]
    [ProducesResponseType(typeof(JobNoteStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JobNoteStatsDto>> GetJobNoteStats(
        [FromRoute] Guid serviceJobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (total, customerVisible, sensitive, deleted) = await _jobNoteService.GetStatsAsync(serviceJobId, cancellationToken);

            var stats = new JobNoteStatsDto
            {
                TotalNotes = total,
                CustomerVisibleNotes = customerVisible,
                SensitiveNotes = sensitive,
                DeletedNotes = deleted
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job note stats for service job {ServiceJobId}", serviceJobId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets customer-safe notes for a service job
    /// </summary>
    [HttpGet("customer-view")]
    [AllowAnonymous] // Customers might access this without full auth
    [ProducesResponseType(typeof(List<CustomerJobNoteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CustomerJobNoteDto>>> GetCustomerNotes(
        [FromRoute] Guid serviceJobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notes = await _jobNoteService.FilterAsync(
                serviceJobId,
                isCustomerVisible: true,
                isSensitive: false,
                includeDeleted: false,
                cancellationToken: cancellationToken);

            var customerNotes = notes.Select(note => new CustomerJobNoteDto
            {
                Id = note.Id,
                Content = note.Content,
                Type = note.Type,
                AuthorName = note.AuthorName,
                CreatedAt = note.CreatedAt
            }).ToList();

            return Ok(customerNotes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customer notes for service job {ServiceJobId}", serviceJobId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    #region Helper Methods

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("id");
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) 
            ? userId 
            : throw new InvalidOperationException("User ID not found in claims");
    }

    private string GetCurrentUserName()
    {
        return User.FindFirst("name")?.Value ?? 
               User.FindFirst("preferred_username")?.Value ?? 
               User.Identity?.Name ?? 
               "Unknown User";
    }

    private string? GetCurrentUserRole()
    {
        return User.FindFirst("role")?.Value;
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        return HttpContext.Request.Headers["User-Agent"].FirstOrDefault();
    }

    private string? GetSessionId()
    {
        return HttpContext.Session?.Id;
    }

    private bool HasPermission(string permission)
    {
        // Simplified permission check - in a real implementation, this would check
        // against a proper authorization service
        return User.IsInRole("Admin") || 
               User.IsInRole("Manager") || 
               User.HasClaim("permission", permission);
    }

    #endregion
}