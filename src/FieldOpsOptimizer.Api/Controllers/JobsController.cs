using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Exceptions;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// API controller for service job management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JobsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<JobsController> _logger;

    public JobsController(ApplicationDbContext context, ILogger<JobsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all service jobs with optional filtering
    /// </summary>
    /// <param name="status">Filter by job status</param>
    /// <param name="technicianId">Filter by assigned technician</param>
    /// <param name="date">Filter by scheduled date</param>
    /// <param name="priority">Filter by priority level</param>
    /// <param name="pageNumber">Page number for pagination</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of service jobs</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedJobsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedJobsResponse>> GetJobs(
        [FromQuery] JobStatus? status = null,
        [FromQuery] Guid? technicianId = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] JobPriority? priority = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.ServiceJobs.AsQueryable();

            // Apply filters
            if (status.HasValue)
                query = query.Where(j => j.Status == status.Value);

            if (technicianId.HasValue)
                query = query.Where(j => j.AssignedTechnicianId == technicianId.Value);

            if (date.HasValue)
                query = query.Where(j => j.ScheduledDate.Date == date.Value.Date);

            if (priority.HasValue)
                query = query.Where(j => j.Priority == priority.Value);

            // Get total count for pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply pagination
            var jobs = await query
                .OrderBy(j => j.ScheduledDate)
                .ThenBy(j => j.Priority)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var response = new PaginatedJobsResponse
            {
                Jobs = jobs.Select(MapToJobResponse).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving jobs");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets a specific service job by ID
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service job details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobResponse>> GetJob(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _context.ServiceJobs
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

            if (job == null)
            {
                throw new EntityNotFoundException(nameof(ServiceJob), id);
            }

            return Ok(MapToJobResponse(job));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving job {JobId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Creates a new service job
    /// </summary>
    /// <param name="request">Job creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created job details</returns>
    [HttpPost]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobResponse>> CreateJob(
        [FromBody] CreateJobRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating new job for customer {CustomerName}", request.CustomerName);

            // Validate request
            var validation = ValidateJobRequest(request);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.ToDictionary(
                    e => e.Split(':')[0], 
                    e => new[] { e.Split(':', 2).Length > 1 ? e.Split(':', 2)[1].Trim() : e });
                throw new ValidationException(errors);
            }

            // Create job entity using constructor
            var jobNumber = await GenerateJobNumber();
            var serviceAddress = new Address(
                request.Address.Street,
                request.Address.City,
                request.Address.State, 
                request.Address.PostalCode,
                request.Address.Country,
                "", // formatted address - will be computed
                request.Address.Coordinate);

            var job = new ServiceJob(
                jobNumber,
                request.CustomerName,
                serviceAddress,
                request.Description,
                request.ScheduledDate,
                request.EstimatedDuration,
                "default-tenant", // TODO: Get from authenticated user context
                request.Priority);

            // Set additional properties using domain methods
            if (request.PreferredTimeWindow.HasValue)
            {
                job.UpdateSchedule(request.ScheduledDate, request.PreferredTimeWindow.Value);
            }

            // Add skills and tags
            foreach (var skill in request.RequiredSkills)
            {
                job.AddRequiredSkill(skill);
            }

            foreach (var tag in request.Tags)
            {
                job.AddTag(tag);
            }

            _context.ServiceJobs.Add(job);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created job {JobNumber} with ID {JobId}", job.JobNumber, job.Id);

            var response = MapToJobResponse(job);
            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Updates an existing service job
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="request">Job update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated job details</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobResponse>> UpdateJob(
        Guid id,
        [FromBody] UpdateJobRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _context.ServiceJobs
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

            if (job == null)
            {
                throw new EntityNotFoundException(nameof(ServiceJob), id);
            }

            _logger.LogInformation("Updating job {JobNumber}", job.JobNumber);

            // Update job properties using domain methods
            if (!string.IsNullOrEmpty(request.Description))
            {
                job.UpdateServiceDetails(request.Description);
            }

            if (request.Priority.HasValue)
            {
                job.UpdatePriority(request.Priority.Value);
            }

            if (request.Status.HasValue)
            {
                job.UpdateStatus(request.Status.Value);
            }

            if (request.ScheduledDate.HasValue)
            {
                job.UpdateSchedule(request.ScheduledDate.Value, request.PreferredTimeWindow);
            }
            else if (request.PreferredTimeWindow.HasValue)
            {
                job.UpdateSchedule(job.ScheduledDate, request.PreferredTimeWindow.Value);
            }

            if (request.AssignedTechnicianId.HasValue)
            {
                job.AssignTechnician(request.AssignedTechnicianId.Value);
            }

            // Handle skills update - remove old skills and add new ones
            if (request.RequiredSkills?.Any() == true)
            {
                // Clear existing skills and add new ones
                foreach (var existingSkill in job.RequiredSkills.ToList())
                {
                    job.RemoveRequiredSkill(existingSkill);
                }
                foreach (var newSkill in request.RequiredSkills)
                {
                    job.AddRequiredSkill(newSkill);
                }
            }

            // Handle tags update - remove old tags and add new ones
            if (request.Tags?.Any() == true)
            {
                // Clear existing tags and add new ones
                foreach (var existingTag in job.Tags.ToList())
                {
                    job.RemoveTag(existingTag);
                }
                foreach (var newTag in request.Tags)
                {
                    job.AddTag(newTag);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(MapToJobResponse(job));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job {JobId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Assigns a job to a technician
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="request">Assignment request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated job details</returns>
    [HttpPost("{id}/assign")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobResponse>> AssignJob(
        Guid id,
        [FromBody] AssignJobRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _context.ServiceJobs
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

            if (job == null)
            {
                throw new EntityNotFoundException(nameof(ServiceJob), id);
            }

            // Validate technician exists
            var technician = await _context.Technicians
                .FirstOrDefaultAsync(t => t.Id == request.TechnicianId, cancellationToken);

            if (technician == null)
            {
                throw new EntityNotFoundException(nameof(Technician), request.TechnicianId);
            }

            // Validate technician skills if required
            if (job.RequiredSkills.Any())
            {
                var missingSkills = job.RequiredSkills.Except(technician.Skills).ToList();
                if (missingSkills.Any())
                {
                    throw new BusinessRuleValidationException(
                        "TechnicianSkillsValidation",
                        $"Technician is missing required skills: {string.Join(", ", missingSkills)}");
                }
            }

            _logger.LogInformation("Assigning job {JobNumber} to technician {TechnicianId}", 
                job.JobNumber, request.TechnicianId);

            job.AssignTechnician(request.TechnicianId);

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(MapToJobResponse(job));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning job {JobId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Updates the status of a service job
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="request">Status update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated job details</returns>
    [HttpPost("{id}/status")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobResponse>> UpdateJobStatus(
        Guid id,
        [FromBody] UpdateJobStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _context.ServiceJobs
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

            if (job == null)
            {
                throw new EntityNotFoundException(nameof(ServiceJob), id);
            }

            _logger.LogInformation("Updating job {JobNumber} status from {OldStatus} to {NewStatus}", 
                job.JobNumber, job.Status, request.Status);

            job.UpdateStatus(request.Status);

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(MapToJobResponse(job));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job status for job {JobId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Deletes a service job
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteJob(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await _context.ServiceJobs
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

            if (job == null)
            {
                throw new EntityNotFoundException(nameof(ServiceJob), id);
            }

            // Prevent deletion of jobs that are in progress or completed
            if (job.Status == JobStatus.InProgress || job.Status == JobStatus.Completed)
            {
                throw new InvalidEntityStateException(
                    nameof(ServiceJob), 
                    job.Status.ToString(), 
                    "delete");
            }

            _logger.LogInformation("Deleting job {JobNumber}", job.JobNumber);

            _context.ServiceJobs.Remove(job);
            await _context.SaveChangesAsync(cancellationToken);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting job {JobId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Searches jobs by various criteria
    /// </summary>
    /// <param name="request">Search request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching jobs</returns>
    [HttpPost("search")]
    [ProducesResponseType(typeof(List<JobResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobResponse>>> SearchJobs(
        [FromBody] SearchJobsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.ServiceJobs.AsQueryable();

            // Text search
            if (!string.IsNullOrEmpty(request.SearchText))
            {
                var searchLower = request.SearchText.ToLower();
                query = query.Where(j => 
                    j.CustomerName.ToLower().Contains(searchLower) ||
                    j.Description.ToLower().Contains(searchLower) ||
                    j.JobNumber.ToLower().Contains(searchLower));
            }

            // Location search (within radius)
            if (request.Location != null && request.RadiusKm > 0)
            {
                // This is a simplified distance calculation - in production, use spatial queries
                query = query.Where(j => 
                    Math.Abs(j.ServiceAddress.Coordinate!.Latitude - request.Location.Latitude) < request.RadiusKm * 0.009 &&
                    Math.Abs(j.ServiceAddress.Coordinate.Longitude - request.Location.Longitude) < request.RadiusKm * 0.009);
            }

            // Date range
            if (request.DateFrom.HasValue)
                query = query.Where(j => j.ScheduledDate >= request.DateFrom.Value);

            if (request.DateTo.HasValue)
                query = query.Where(j => j.ScheduledDate <= request.DateTo.Value);

            // Skills filter
            if (request.RequiredSkills?.Any() == true)
            {
                foreach (var skill in request.RequiredSkills)
                {
                    query = query.Where(j => j.RequiredSkills.Contains(skill));
                }
            }

            var jobs = await query
                .OrderBy(j => j.ScheduledDate)
                .Take(100) // Limit results
                .ToListAsync(cancellationToken);

            return Ok(jobs.Select(MapToJobResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching jobs");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets jobs that are available for assignment to a specific technician
    /// </summary>
    /// <param name="technicianId">Technician ID</param>
    /// <param name="date">Date to check availability for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Available jobs for the technician</returns>
    [HttpGet("available/{technicianId}")]
    [ProducesResponseType(typeof(List<JobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<JobResponse>>> GetAvailableJobs(
        Guid technicianId,
        [FromQuery] DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetDate = date ?? DateTime.Today;

            // Verify technician exists
            var technician = await _context.Technicians
                .FirstOrDefaultAsync(t => t.Id == technicianId, cancellationToken);

            if (technician == null)
            {
                throw new EntityNotFoundException(nameof(Technician), technicianId);
            }

            // Get unassigned jobs that match technician skills
            var availableJobs = await _context.ServiceJobs
                .Where(j => j.AssignedTechnicianId == null)
                .Where(j => j.Status == JobStatus.Scheduled)
                .Where(j => j.ScheduledDate.Date == targetDate.Date)
                .Where(j => j.RequiredSkills.All(skill => technician.Skills.Contains(skill)))
                .OrderBy(j => j.Priority)
                .ThenBy(j => j.ScheduledDate)
                .ToListAsync(cancellationToken);

            return Ok(availableJobs.Select(MapToJobResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available jobs for technician {TechnicianId}", technicianId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private (bool IsValid, List<string> Errors) ValidateJobRequest(CreateJobRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            errors.Add("Customer name is required");

        if (string.IsNullOrWhiteSpace(request.Description))
            errors.Add("Job description is required");

        if (request.EstimatedDuration <= TimeSpan.Zero)
            errors.Add("Estimated duration must be positive");

        if (request.EstimatedRevenue < 0)
            errors.Add("Estimated revenue cannot be negative");

        if (request.ScheduledDate < DateTime.Today)
            errors.Add("Scheduled date cannot be in the past");

        if (request.Address == null)
            errors.Add("Service address is required");

        return (IsValid: !errors.Any(), Errors: errors);
    }

    private async Task<string> GenerateJobNumber()
    {
        var today = DateTime.Today;
        var prefix = $"JOB{today:yyyyMMdd}";
        
        var existingCount = await _context.ServiceJobs
            .CountAsync(j => j.JobNumber.StartsWith(prefix));

        return $"{prefix}-{(existingCount + 1):D3}";
    }

    private static JobResponse MapToJobResponse(ServiceJob job)
    {
        return new JobResponse
        {
            Id = job.Id,
            JobNumber = job.JobNumber,
            CustomerName = job.CustomerName,
            Description = job.Description,
            Address = new AddressResponse
            {
                Street = job.ServiceAddress.Street,
                City = job.ServiceAddress.City,
                State = job.ServiceAddress.State,
                PostalCode = job.ServiceAddress.PostalCode,
                Country = job.ServiceAddress.Country,
                FormattedAddress = job.ServiceAddress.FormattedAddress,
                Coordinate = job.ServiceAddress.Coordinate
            },
            EstimatedDuration = job.EstimatedDuration,
            EstimatedRevenue = job.EstimatedRevenue,
            Priority = job.Priority,
            Status = job.Status,
            RequiredSkills = job.RequiredSkills.ToList(),
            Tags = job.Tags.ToList(),
            ScheduledDate = job.ScheduledDate,
            PreferredTimeWindow = job.PreferredTimeWindow,
            AssignedTechnicianId = job.AssignedTechnicianId,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            CompletedAt = job.CompletedAt
        };
    }
}

// Request/Response DTOs
public record CreateJobRequest
{
    /// <summary>
    /// Customer name for the service job
    /// </summary>
    public required string CustomerName { get; init; }

    /// <summary>
    /// Service address where the job will be performed
    /// </summary>
    public required AddressRequest Address { get; init; }

    /// <summary>
    /// Description of the work to be performed
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Estimated duration for the job
    /// </summary>
    public required TimeSpan EstimatedDuration { get; init; }

    /// <summary>
    /// Estimated revenue from the job
    /// </summary>
    public decimal EstimatedRevenue { get; init; }

    /// <summary>
    /// Priority level of the job
    /// </summary>
    public JobPriority Priority { get; init; } = JobPriority.Medium;

    /// <summary>
    /// Skills required to complete the job
    /// </summary>
    public List<string> RequiredSkills { get; init; } = new();

    /// <summary>
    /// Tags for categorizing the job
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Scheduled date for the job
    /// </summary>
    public required DateTime ScheduledDate { get; init; }

    /// <summary>
    /// Preferred time window for the job
    /// </summary>
    public TimeSpan? PreferredTimeWindow { get; init; }
}

public record UpdateJobRequest
{
    public string? Description { get; init; }
    public TimeSpan? EstimatedDuration { get; init; }
    public decimal? EstimatedRevenue { get; init; }
    public JobPriority? Priority { get; init; }
    public JobStatus? Status { get; init; }
    public DateTime? ScheduledDate { get; init; }
    public TimeSpan? PreferredTimeWindow { get; init; }
    public Guid? AssignedTechnicianId { get; init; }
    public List<string>? RequiredSkills { get; init; }
    public List<string>? Tags { get; init; }
}

public record AssignJobRequest
{
    /// <summary>
    /// ID of the technician to assign the job to
    /// </summary>
    public required Guid TechnicianId { get; init; }
}

public record UpdateJobStatusRequest
{
    /// <summary>
    /// New status for the job
    /// </summary>
    public required JobStatus Status { get; init; }
}

public record SearchJobsRequest
{
    /// <summary>
    /// Text to search in customer name, description, or job number
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// Location to search around
    /// </summary>
    public Coordinate? Location { get; init; }

    /// <summary>
    /// Search radius in kilometers
    /// </summary>
    public double RadiusKm { get; init; } = 10;

    /// <summary>
    /// Start date range
    /// </summary>
    public DateTime? DateFrom { get; init; }

    /// <summary>
    /// End date range
    /// </summary>
    public DateTime? DateTo { get; init; }

    /// <summary>
    /// Required skills filter
    /// </summary>
    public List<string>? RequiredSkills { get; init; }
}

public record JobResponse
{
    public Guid Id { get; init; }
    public string JobNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public AddressResponse Address { get; init; } = new();
    public TimeSpan EstimatedDuration { get; init; }
    public decimal EstimatedRevenue { get; init; }
    public JobPriority Priority { get; init; }
    public JobStatus Status { get; init; }
    public List<string> RequiredSkills { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public DateTime ScheduledDate { get; init; }
    public TimeSpan? PreferredTimeWindow { get; init; }
    public Guid? AssignedTechnicianId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public record AddressRequest
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string PostalCode { get; init; }
    public string Country { get; init; } = "USA";
    public Coordinate? Coordinate { get; init; }
}

public record AddressResponse
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string FormattedAddress { get; init; } = string.Empty;
    public Coordinate? Coordinate { get; init; }
}

public record PaginatedJobsResponse
{
    public List<JobResponse> Jobs { get; init; } = new();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
