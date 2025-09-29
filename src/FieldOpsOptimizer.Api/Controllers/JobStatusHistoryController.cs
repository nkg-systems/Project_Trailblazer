using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Api.DTOs;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using AutoMapper;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// API controller for job status history management and analytics
/// </summary>
[ApiController]
[Route("api/service-jobs/{serviceJobId}/status-history")]
[Route("api/job-status-history")]
[Authorize]
[Produces("application/json")]
public class JobStatusHistoryController : ControllerBase
{
    private readonly IJobStatusHistoryService _statusHistoryService;
    private readonly IMapper _mapper;
    private readonly ILogger<JobStatusHistoryController> _logger;

    public JobStatusHistoryController(
        IJobStatusHistoryService statusHistoryService,
        IMapper mapper,
        ILogger<JobStatusHistoryController> logger)
    {
        _statusHistoryService = statusHistoryService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Gets status history with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<JobStatusHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobStatusHistoryDto>>> GetStatusHistory(
        [FromRoute] Guid? serviceJobId = null,
        [FromQuery] string? jobNumber = null,
        [FromQuery] JobStatus? fromStatus = null,
        [FromQuery] JobStatus? toStatus = null,
        [FromQuery] Guid? changedByUserId = null,
        [FromQuery] string? changedByUserName = null,
        [FromQuery] DateTime? changedFrom = null,
        [FromQuery] DateTime? changedTo = null,
        [FromQuery] bool? isAutomaticChange = null,
        [FromQuery] string? changeSource = null,
        [FromQuery] int? minDurationMinutes = null,
        [FromQuery] int? maxDurationMinutes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _statusHistoryService.FilterAsync(
                serviceJobId,
                jobNumber,
                fromStatus,
                toStatus,
                changedByUserId,
                changedByUserName,
                changedFrom,
                changedTo,
                isAutomaticChange,
                changeSource,
                minDurationMinutes,
                maxDurationMinutes,
                cancellationToken);

            var dtos = _mapper.Map<List<JobStatusHistoryDto>>(history);
            
            // Filter audit information based on permissions
            if (!HasPermission("ViewAuditTrail"))
            {
                foreach (var dto in dtos)
                {
                    dto.IpAddress = null;
                    dto.UserAgent = null;
                    dto.SessionId = null;
                }
            }

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status history for service job {ServiceJobId}", serviceJobId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets status history for a specific service job
    /// </summary>
    [HttpGet("by-job")]
    [ProducesResponseType(typeof(List<JobStatusHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobStatusHistoryDto>>> GetStatusHistoryByJob(
        [FromRoute] Guid serviceJobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _statusHistoryService.GetByServiceJobAsync(serviceJobId, cancellationToken);
            var dtos = _mapper.Map<List<JobStatusHistoryDto>>(history);
            
            // Filter audit information based on permissions
            if (!HasPermission("ViewAuditTrail"))
            {
                foreach (var dto in dtos)
                {
                    dto.IpAddress = null;
                    dto.UserAgent = null;
                    dto.SessionId = null;
                }
            }

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status history for service job {ServiceJobId}", serviceJobId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Records a new status transition
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobStatusHistoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobStatusHistoryDto>> RecordStatusTransition(
        [FromBody] CreateJobStatusHistoryDto request,
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
            
            _logger.LogInformation("Recording status transition for service job {ServiceJobId} from {FromStatus} to {ToStatus} by user {UserId}", 
                targetServiceJobId, request.FromStatus, request.ToStatus, currentUserId);

            var history = await _statusHistoryService.RecordAsync(
                targetServiceJobId,
                request.JobNumber,
                request.FromStatus,
                request.ToStatus,
                currentUserId,
                currentUserName,
                currentUserRole,
                request.Reason,
                request.IsAutomaticChange,
                request.ChangeSource,
                GetClientIpAddress(),
                GetUserAgent(),
                GetSessionId(),
                request.PreviousStatusDurationMinutes.HasValue 
                    ? TimeSpan.FromMinutes(request.PreviousStatusDurationMinutes.Value) 
                    : null,
                request.TriggeredNotifications,
                request.ValidationWarnings,
                request.AppliedBusinessRules,
                cancellationToken);

            var dto = _mapper.Map<JobStatusHistoryDto>(history);
            
            _logger.LogInformation("Recorded status transition {HistoryId} for service job {ServiceJobId}", 
                history.Id, targetServiceJobId);

            return CreatedAtAction(nameof(GetStatusHistoryByJob), new { serviceJobId = targetServiceJobId }, dto);
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
            _logger.LogError(ex, "Error recording status transition");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets statistics for status transitions
    /// </summary>
    [HttpGet("../stats")]
    [ProducesResponseType(typeof(JobStatusStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JobStatusStatsDto>> GetStatusStats(
        [FromRoute] Guid serviceJobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (totalTransitions, automatic, manual) = await _statusHistoryService.GetStatsAsync(serviceJobId, cancellationToken);

            var stats = new JobStatusStatsDto
            {
                TotalTransitions = totalTransitions,
                AutomaticTransitions = automatic,
                ManualTransitions = manual
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status history stats for service job {ServiceJobId}", serviceJobId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets a timeline of status transitions for a service job
    /// </summary>
    [HttpGet("timeline")]
    [ProducesResponseType(typeof(StatusTransitionTimelineDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatusTransitionTimelineDto>> GetStatusTimeline(
        [FromRoute] Guid serviceJobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _statusHistoryService.GetByServiceJobAsync(serviceJobId, cancellationToken);
            
            var events = new List<StatusTransitionEventDto>();
            JobStatusHistory? previousEvent = null;

            foreach (var historyItem in history.OrderBy(h => h.ChangedAt))
            {
                if (previousEvent != null)
                {
                    // Close the previous status period
                    var lastEvent = events.LastOrDefault(e => e.Status == previousEvent.ToStatus);
                    if (lastEvent != null)
                    {
                        lastEvent.EndedAt = historyItem.ChangedAt;
                        lastEvent.Duration = historyItem.ChangedAt - lastEvent.StartedAt;
                    }
                }

                // Add new status period
                events.Add(new StatusTransitionEventDto
                {
                    Status = historyItem.ToStatus,
                    StartedAt = historyItem.ChangedAt,
                    ChangedByUserName = historyItem.ChangedByUserName,
                    Reason = historyItem.Reason,
                    IsAutomaticChange = historyItem.IsAutomaticChange,
                    IsCurrentStatus = false // Will be updated below
                });

                previousEvent = historyItem;
            }

            // Mark the last status as current
            if (events.Any())
            {
                events.Last().IsCurrentStatus = true;
            }

            var timeline = new StatusTransitionTimelineDto
            {
                ServiceJobId = serviceJobId,
                Events = events,
                TotalTransitions = history.Count,
                TotalJobDuration = events.Any() 
                    ? DateTime.UtcNow - events.First().StartedAt 
                    : TimeSpan.Zero,
                JobCreatedAt = events.FirstOrDefault()?.StartedAt ?? DateTime.UtcNow,
                JobCompletedAt = events.LastOrDefault()?.Status == JobStatus.Completed 
                    ? events.LastOrDefault()?.StartedAt 
                    : null
            };

            return Ok(timeline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status timeline for service job {ServiceJobId}", serviceJobId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets performance analytics for status transitions
    /// </summary>
    [HttpGet("performance")]
    [ProducesResponseType(typeof(List<StatusTransitionPerformanceDto>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<List<StatusTransitionPerformanceDto>>> GetPerformanceAnalytics(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically involve more complex analytics queries
            // For now, returning a simplified structure
            var history = await _statusHistoryService.FilterAsync(
                changedFrom: fromDate,
                changedTo: toDate,
                cancellationToken: cancellationToken);

            var performanceByStatus = history
                .Where(h => h.PreviousStatusDurationMinutes.HasValue)
                .GroupBy(h => h.FromStatus)
                .Select(g => new StatusTransitionPerformanceDto
                {
                    Status = g.Key,
                    TotalJobs = g.Count(),
                    AverageDurationHours = g.Average(h => h.PreviousStatusDurationMinutes!.Value) / 60.0,
                    MedianDurationHours = GetMedian(g.Select(h => h.PreviousStatusDurationMinutes!.Value)) / 60.0,
                    MinDurationHours = g.Min(h => h.PreviousStatusDurationMinutes!.Value) / 60.0,
                    MaxDurationHours = g.Max(h => h.PreviousStatusDurationMinutes!.Value) / 60.0,
                    StandardDeviation = GetStandardDeviation(g.Select(h => h.PreviousStatusDurationMinutes!.Value).Select(v => v / 60.0)),
                    // These would be calculated based on business SLA rules
                    JobsExceedingSLA = 0,
                    SLAComplianceRate = 100.0
                })
                .ToList();

            return Ok(performanceByStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance analytics");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Validates a status transition before execution
    /// </summary>
    [HttpPost("validate-transition")]
    [ProducesResponseType(typeof(StatusTransitionValidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatusTransitionValidationResultDto>> ValidateTransition(
        [FromBody] ValidateStatusTransitionDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // This would use the JobStatusTransitionService for validation
            // For now, returning a basic validation structure
            var result = new StatusTransitionValidationResultDto
            {
                IsValid = true,
                Warnings = new List<string>(),
                Errors = new List<string>(),
                RequiredPermissions = new List<string>(),
                BusinessRulesToApply = new List<string>(),
                NotificationsToTrigger = new List<string>()
            };

            // Basic validation logic would go here
            if (request.FromStatus == request.ToStatus)
            {
                result.IsValid = false;
                result.Errors.Add("Source and target status cannot be the same");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating status transition");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets status transition summary for a service job
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(List<JobStatusHistorySummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobStatusHistorySummaryDto>>> GetStatusSummary(
        [FromRoute] Guid serviceJobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _statusHistoryService.GetByServiceJobAsync(serviceJobId, cancellationToken);
            
            var summaries = history.Select(h => new JobStatusHistorySummaryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                ChangedAt = h.ChangedAt,
                ChangedByUserName = h.ChangedByUserName,
                Reason = h.Reason,
                IsAutomaticChange = h.IsAutomaticChange,
                PreviousStatusDuration = h.GetPreviousStatusDuration()
            }).ToList();

            return Ok(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status summary for service job {ServiceJobId}", serviceJobId);
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

    private static double GetMedian(IEnumerable<int> values)
    {
        var sortedValues = values.OrderBy(x => x).ToList();
        if (!sortedValues.Any()) return 0;
        
        if (sortedValues.Count % 2 == 0)
        {
            return (sortedValues[sortedValues.Count / 2 - 1] + sortedValues[sortedValues.Count / 2]) / 2.0;
        }
        else
        {
            return sortedValues[sortedValues.Count / 2];
        }
    }

    private static double GetStandardDeviation(IEnumerable<double> values)
    {
        var valuesList = values.ToList();
        if (!valuesList.Any()) return 0;
        
        var average = valuesList.Average();
        var sumOfSquaresOfDifferences = valuesList.Select(val => (val - average) * (val - average)).Sum();
        return Math.Sqrt(sumOfSquaresOfDifferences / valuesList.Count);
    }

    #endregion
}