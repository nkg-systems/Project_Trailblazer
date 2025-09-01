using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// API controller for technician management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TechniciansController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TechniciansController> _logger;

    public TechniciansController(ApplicationDbContext context, ILogger<TechniciansController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all technicians with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TechnicianResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TechnicianResponse>>> GetTechnicians(
        [FromQuery] TechnicianStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.Technicians.AsQueryable();

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            var technicians = await query
                .OrderBy(t => t.LastName)
                .ThenBy(t => t.FirstName)
                .ToListAsync(cancellationToken);

            return Ok(technicians.Select(MapToTechnicianResponse).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technicians");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets a specific technician by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TechnicianResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TechnicianResponse>> GetTechnician(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var technician = await _context.Technicians
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (technician == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Technician not found",
                    Detail = $"Technician with ID {id} was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(MapToTechnicianResponse(technician));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician {TechnicianId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Creates a new technician
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TechnicianResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TechnicianResponse>> CreateTechnician(
        [FromBody] CreateTechnicianRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating new technician {FirstName} {LastName}", request.FirstName, request.LastName);

            // Check if employee ID already exists
            var existingTechnician = await _context.Technicians
                .FirstOrDefaultAsync(t => t.EmployeeId == request.EmployeeId, cancellationToken);

            if (existingTechnician != null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Employee ID already exists",
                    Detail = $"A technician with employee ID {request.EmployeeId} already exists"
                });
            }

            // Create technician entity using constructor
            var technician = new Technician(
                request.EmployeeId,
                request.FirstName,
                request.LastName,
                request.Email,
                "default-tenant", // TODO: Get from authenticated user context
                request.HourlyRate
            );

            // Set additional properties using domain methods
            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                technician.UpdateContactInfo(request.FirstName, request.LastName, request.Email, request.PhoneNumber);
            }

            if (request.HomeAddress != null)
            {
                var homeAddress = new Address(
                    request.HomeAddress.Street,
                    request.HomeAddress.City,
                    request.HomeAddress.State,
                    request.HomeAddress.PostalCode,
                    request.HomeAddress.Country ?? "USA",
                    "", // formatted address - will be computed
                    request.HomeAddress.Coordinate);
                
                technician.SetHomeAddress(homeAddress);
            }

            // Add skills
            foreach (var skill in request.Skills)
            {
                technician.AddSkill(skill);
            }

            // Set working hours
            if (request.WorkingHours?.Any() == true)
            {
                var workingHours = request.WorkingHours.Select(wh => new WorkingHours(
                    wh.DayOfWeek,
                    wh.StartTime,
                    wh.EndTime
                )).ToList();

                technician.SetWorkingHours(workingHours);
            }

            _context.Technicians.Add(technician);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created technician {EmployeeId} with ID {TechnicianId}", 
                technician.EmployeeId, technician.Id);

            var response = MapToTechnicianResponse(technician);
            return CreatedAtAction(nameof(GetTechnician), new { id = technician.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating technician");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Updates a technician's location
    /// </summary>
    [HttpPost("{id}/location")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTechnicianLocation(
        Guid id,
        [FromBody] UpdateLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var technician = await _context.Technicians
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (technician == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Technician not found",
                    Detail = $"Technician with ID {id} was not found"
                });
            }

            technician.UpdateLocation(request.Location);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Updated location for technician {EmployeeId}", technician.EmployeeId);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating technician location {TechnicianId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static TechnicianResponse MapToTechnicianResponse(Technician technician)
    {
        return new TechnicianResponse
        {
            Id = technician.Id,
            EmployeeId = technician.EmployeeId,
            FirstName = technician.FirstName,
            LastName = technician.LastName,
            Email = technician.Email,
            PhoneNumber = technician.Phone ?? "",
            HourlyRate = technician.HourlyRate,
            Status = technician.Status,
            Skills = technician.Skills.ToList(),
            HomeAddress = technician.HomeAddress != null ? new AddressResponse
            {
                Street = technician.HomeAddress.Street,
                City = technician.HomeAddress.City,
                State = technician.HomeAddress.State,
                PostalCode = technician.HomeAddress.PostalCode,
                Country = technician.HomeAddress.Country,
                FormattedAddress = technician.HomeAddress.FormattedAddress,
                Coordinate = technician.HomeAddress.Coordinate
            } : new AddressResponse(),
            CurrentLocation = technician.CurrentLocation,
            LastLocationUpdate = technician.LastLocationUpdate,
            WorkingHours = technician.WorkingHours.Select(wh => new WorkingHoursResponse
            {
                DayOfWeek = wh.DayOfWeek,
                StartTime = wh.StartTime,
                EndTime = wh.EndTime
            }).ToList(),
            CreatedAt = technician.CreatedAt,
            UpdatedAt = technician.UpdatedAt
        };
    }
}

// Request/Response DTOs
public record CreateTechnicianRequest
{
    public required string EmployeeId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public string? PhoneNumber { get; init; }
    public decimal HourlyRate { get; init; }
    public List<string> Skills { get; init; } = new();
    public AddressRequest? HomeAddress { get; init; }
    public List<WorkingHoursRequest>? WorkingHours { get; init; }
}

public record UpdateLocationRequest
{
    public required Coordinate Location { get; init; }
}

public record WorkingHoursRequest
{
    public required DayOfWeek DayOfWeek { get; init; }
    public required TimeSpan StartTime { get; init; }
    public required TimeSpan EndTime { get; init; }
}

public record TechnicianResponse
{
    public Guid Id { get; init; }
    public string EmployeeId { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public decimal HourlyRate { get; init; }
    public TechnicianStatus Status { get; init; }
    public List<string> Skills { get; init; } = new();
    public AddressResponse HomeAddress { get; init; } = new();
    public Coordinate? CurrentLocation { get; init; }
    public DateTime? LastLocationUpdate { get; init; }
    public List<WorkingHoursResponse> WorkingHours { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record WorkingHoursResponse
{
    public DayOfWeek DayOfWeek { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
}

// AddressRequest and AddressResponse are defined in JobsController
