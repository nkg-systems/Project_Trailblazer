using Microsoft.AspNetCore.Mvc;
using FieldOpsOptimizer.Infrastructure.Optimization;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// API controller for route optimization operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RouteOptimizationController : ControllerBase
{
    private readonly IRouteOptimizationService _optimizationService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RouteOptimizationController> _logger;

    public RouteOptimizationController(
        IRouteOptimizationService optimizationService,
        ApplicationDbContext context,
        ILogger<RouteOptimizationController> logger)
    {
        _optimizationService = optimizationService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Optimizes a route using the specified algorithm
    /// </summary>
    /// <param name="request">Route optimization request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimized route result</returns>
    [HttpPost("optimize")]
    [ProducesResponseType(typeof(RouteOptimizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RouteOptimizationResponse>> OptimizeRoute(
        [FromBody] OptimizeRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Optimizing route for technician {TechnicianId} with {JobCount} jobs using {Algorithm}",
                request.TechnicianId, request.JobIds.Count, request.Algorithm);

            // Validate request
            var validation = await ValidateOptimizationRequest(request);
            if (!validation.IsValid)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid optimization request",
                    Detail = string.Join("; ", validation.Errors),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Load technician and jobs
            var technician = await _context.Technicians
                .FirstOrDefaultAsync(t => t.Id == request.TechnicianId, cancellationToken);

            if (technician == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Technician not found",
                    Detail = $"Technician with ID {request.TechnicianId} was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            var jobs = await _context.ServiceJobs
                .Where(j => request.JobIds.Contains(j.Id))
                .ToListAsync(cancellationToken);

            if (jobs.Count != request.JobIds.Count)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Some jobs not found",
                    Detail = "One or more job IDs were not found",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Build optimization parameters
            var parameters = new RouteOptimizationParameters
            {
                Jobs = jobs,
                Technician = technician,
                Objective = request.Objective,
                RespectTimeWindows = request.RespectTimeWindows,
                ValidateSkills = request.ValidateSkills,
                StartLocation = request.StartLocation ?? technician.HomeAddress.Coordinate,
                EndLocation = request.EndLocation,
                MaxOptimizationTimeSeconds = request.MaxOptimizationTimeSeconds
            };

            // Run optimization
            var result = await _optimizationService.OptimizeRouteAsync(
                parameters, 
                request.Algorithm, 
                cancellationToken);

            // Convert to response model
            var response = new RouteOptimizationResponse
            {
                OptimizationId = Guid.NewGuid(),
                Algorithm = result.Algorithm,
                TotalDistanceKm = result.TotalDistanceKm,
                TotalDuration = result.TotalDuration,
                TotalCost = result.TotalCost,
                OptimizationTime = result.OptimizationTime,
                IsOptimal = result.IsOptimal,
                ConstraintViolations = result.ConstraintViolations.ToList(),
                OptimizedStops = result.OptimizedStops.Select(s => new OptimizedStopResponse
                {
                    JobId = s.Job.Id,
                    JobNumber = s.Job.JobNumber,
                    SequenceOrder = s.SequenceOrder,
                    CustomerName = s.Job.CustomerName,
                    ServiceAddress = s.Job.ServiceAddress.FormattedAddress,
                    DistanceFromPreviousKm = s.DistanceFromPreviousKm,
                    TravelTimeFromPrevious = s.TravelTimeFromPrevious,
                    EstimatedArrival = s.EstimatedArrival,
                    EstimatedDeparture = s.EstimatedDeparture,
                    EstimatedDuration = s.Job.EstimatedDuration,
                    HasConstraintViolations = s.HasConstraintViolations,
                    ConstraintViolations = s.ConstraintViolations.ToList()
                }).ToList(),
                Metrics = new OptimizationMetricsResponse
                {
                    InitialCost = result.Metrics.InitialCost,
                    FinalCost = result.Metrics.FinalCost,
                    ImprovementPercentage = result.Metrics.ImprovementPercentage,
                    Evaluations = result.Metrics.Evaluations,
                    Iterations = result.Iterations
                }
            };

            _logger.LogInformation("Route optimization completed successfully. " +
                "Distance: {Distance:F2}km, Duration: {Duration}, Improvement: {Improvement:F1}%",
                result.TotalDistanceKm, result.TotalDuration, result.Metrics.ImprovementPercentage);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during route optimization for technician {TechnicianId}", request.TechnicianId);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Route optimization failed",
                Detail = "An error occurred during route optimization. Please try again.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Compares multiple optimization algorithms for a route
    /// </summary>
    /// <param name="request">Algorithm comparison request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comparison results for all algorithms</returns>
    [HttpPost("compare-algorithms")]
    [ProducesResponseType(typeof(AlgorithmComparisonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AlgorithmComparisonResponse>> CompareAlgorithms(
        [FromBody] CompareAlgorithmsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Comparing {AlgorithmCount} algorithms for technician {TechnicianId}",
                request.Algorithms.Count, request.TechnicianId);

            // Validate and load data (similar to optimize endpoint)
            var validation = await ValidateOptimizationRequest(new OptimizeRouteRequest
            {
                TechnicianId = request.TechnicianId,
                JobIds = request.JobIds,
                Objective = request.Objective,
                Algorithm = request.Algorithms.FirstOrDefault()
            });

            if (!validation.IsValid)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid comparison request",
                    Detail = string.Join("; ", validation.Errors)
                });
            }

            var technician = await _context.Technicians
                .FirstOrDefaultAsync(t => t.Id == request.TechnicianId, cancellationToken);

            var jobs = await _context.ServiceJobs
                .Where(j => request.JobIds.Contains(j.Id))
                .ToListAsync(cancellationToken);

            var parameters = new RouteOptimizationParameters
            {
                Jobs = jobs,
                Technician = technician!,
                Objective = request.Objective,
                RespectTimeWindows = request.RespectTimeWindows,
                ValidateSkills = request.ValidateSkills,
                StartLocation = request.StartLocation ?? technician!.HomeAddress.Coordinate
            };

            // Run comparison
            var comparison = await _optimizationService.CompareAlgorithmsAsync(
                parameters,
                request.Algorithms,
                cancellationToken);

            // Build response
            var response = new AlgorithmComparisonResponse
            {
                ComparisonId = Guid.NewGuid(),
                BestAlgorithm = comparison.BestResult.Algorithm,
                Results = comparison.AllResults.Select(r => new AlgorithmResultSummary
                {
                    Algorithm = r.Algorithm,
                    TotalDistanceKm = r.TotalDistanceKm,
                    TotalDuration = r.TotalDuration,
                    TotalCost = r.TotalCost,
                    OptimizationTime = r.OptimizationTime,
                    ImprovementPercentage = r.Metrics.ImprovementPercentage,
                    ConstraintViolationCount = r.ConstraintViolations.Count
                }).ToList(),
                ComparisonMetrics = comparison.ComparisonMetrics
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during algorithm comparison");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets available optimization algorithms for a specific objective
    /// </summary>
    /// <param name="objective">Optimization objective</param>
    /// <returns>List of available algorithms</returns>
    [HttpGet("algorithms")]
    [ProducesResponseType(typeof(AvailableAlgorithmsResponse), StatusCodes.Status200OK)]
    public ActionResult<AvailableAlgorithmsResponse> GetAvailableAlgorithms(
        [FromQuery] OptimizationObjective objective = OptimizationObjective.MinimizeDistance)
    {
        var algorithms = _optimizationService.GetAvailableAlgorithms(objective);
        
        return Ok(new AvailableAlgorithmsResponse
        {
            Objective = objective,
            AvailableAlgorithms = algorithms.Select(a => new AlgorithmInfo
            {
                Algorithm = a,
                Name = a.ToString(),
                Description = GetAlgorithmDescription(a),
                Characteristics = GetAlgorithmCharacteristics(a)
            }).ToList()
        });
    }

    private async Task<(bool IsValid, List<string> Errors)> ValidateOptimizationRequest(OptimizeRouteRequest request)
    {
        var errors = new List<string>();

        if (request.TechnicianId == Guid.Empty)
            errors.Add("TechnicianId is required");

        if (!request.JobIds.Any())
            errors.Add("At least one job ID is required");

        if (request.JobIds.Count > 100) // Reasonable limit
            errors.Add("Too many jobs (maximum 100 allowed)");

        // Check if technician exists
        var technicianExists = await _context.Technicians
            .AnyAsync(t => t.Id == request.TechnicianId);

        if (!technicianExists)
            errors.Add($"Technician with ID {request.TechnicianId} not found");

        return (IsValid: !errors.Any(), Errors: errors);
    }

    private static string GetAlgorithmDescription(OptimizationAlgorithm algorithm)
    {
        return algorithm switch
        {
            OptimizationAlgorithm.NearestNeighbor => "Fast greedy algorithm that picks the nearest unvisited job",
            OptimizationAlgorithm.TwoOpt => "Improves routes by eliminating crossing paths",
            OptimizationAlgorithm.Genetic => "Advanced population-based search with high solution quality",
            _ => "Algorithm description not available"
        };
    }

    private static AlgorithmCharacteristics GetAlgorithmCharacteristics(OptimizationAlgorithm algorithm)
    {
        return algorithm switch
        {
            OptimizationAlgorithm.NearestNeighbor => new AlgorithmCharacteristics
            {
                Speed = "Very Fast",
                Quality = "Basic",
                Complexity = "Low",
                BestFor = "Quick solutions with many jobs"
            },
            OptimizationAlgorithm.TwoOpt => new AlgorithmCharacteristics
            {
                Speed = "Fast",
                Quality = "Good", 
                Complexity = "Medium",
                BestFor = "Balanced speed and quality"
            },
            OptimizationAlgorithm.Genetic => new AlgorithmCharacteristics
            {
                Speed = "Slower",
                Quality = "Excellent",
                Complexity = "High",
                BestFor = "Best possible routes with fewer jobs"
            },
            _ => new AlgorithmCharacteristics()
        };
    }
}

// Request/Response DTOs
public record OptimizeRouteRequest
{
    /// <summary>
    /// ID of the technician to optimize the route for
    /// </summary>
    public required Guid TechnicianId { get; init; }

    /// <summary>
    /// List of job IDs to include in the route
    /// </summary>
    public required List<Guid> JobIds { get; init; }

    /// <summary>
    /// Optimization algorithm to use
    /// </summary>
    public OptimizationAlgorithm Algorithm { get; init; } = OptimizationAlgorithm.TwoOpt;

    /// <summary>
    /// Optimization objective
    /// </summary>
    public OptimizationObjective Objective { get; init; } = OptimizationObjective.MinimizeDistance;

    /// <summary>
    /// Whether to respect job time windows
    /// </summary>
    public bool RespectTimeWindows { get; init; } = true;

    /// <summary>
    /// Whether to validate technician skills
    /// </summary>
    public bool ValidateSkills { get; init; } = true;

    /// <summary>
    /// Custom start location (defaults to technician home)
    /// </summary>
    public Coordinate? StartLocation { get; init; }

    /// <summary>
    /// Custom end location
    /// </summary>
    public Coordinate? EndLocation { get; init; }

    /// <summary>
    /// Maximum optimization time in seconds
    /// </summary>
    public int MaxOptimizationTimeSeconds { get; init; } = 30;
}

public record RouteOptimizationResponse
{
    public Guid OptimizationId { get; init; }
    public OptimizationAlgorithm Algorithm { get; init; }
    public double TotalDistanceKm { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public decimal TotalCost { get; init; }
    public TimeSpan OptimizationTime { get; init; }
    public bool IsOptimal { get; init; }
    public List<string> ConstraintViolations { get; init; } = new();
    public List<OptimizedStopResponse> OptimizedStops { get; init; } = new();
    public OptimizationMetricsResponse Metrics { get; init; } = new();
}

public record OptimizedStopResponse
{
    public Guid JobId { get; init; }
    public string JobNumber { get; init; } = string.Empty;
    public int SequenceOrder { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string ServiceAddress { get; init; } = string.Empty;
    public double DistanceFromPreviousKm { get; init; }
    public TimeSpan TravelTimeFromPrevious { get; init; }
    public DateTime EstimatedArrival { get; init; }
    public DateTime EstimatedDeparture { get; init; }
    public TimeSpan EstimatedDuration { get; init; }
    public bool HasConstraintViolations { get; init; }
    public List<string> ConstraintViolations { get; init; } = new();
}

public record OptimizationMetricsResponse
{
    public double InitialCost { get; init; }
    public double FinalCost { get; init; }
    public double ImprovementPercentage { get; init; }
    public int Evaluations { get; init; }
    public int Iterations { get; init; }
}

public record CompareAlgorithmsRequest
{
    public required Guid TechnicianId { get; init; }
    public required List<Guid> JobIds { get; init; }
    public required List<OptimizationAlgorithm> Algorithms { get; init; }
    public OptimizationObjective Objective { get; init; } = OptimizationObjective.MinimizeDistance;
    public bool RespectTimeWindows { get; init; } = true;
    public bool ValidateSkills { get; init; } = true;
    public Coordinate? StartLocation { get; init; }
}

public record AlgorithmComparisonResponse
{
    public Guid ComparisonId { get; init; }
    public OptimizationAlgorithm BestAlgorithm { get; init; }
    public List<AlgorithmResultSummary> Results { get; init; } = new();
    public Dictionary<string, object> ComparisonMetrics { get; init; } = new();
}

public record AlgorithmResultSummary
{
    public OptimizationAlgorithm Algorithm { get; init; }
    public double TotalDistanceKm { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public decimal TotalCost { get; init; }
    public TimeSpan OptimizationTime { get; init; }
    public double ImprovementPercentage { get; init; }
    public int ConstraintViolationCount { get; init; }
}

public record AvailableAlgorithmsResponse
{
    public OptimizationObjective Objective { get; init; }
    public List<AlgorithmInfo> AvailableAlgorithms { get; init; } = new();
}

public record AlgorithmInfo
{
    public OptimizationAlgorithm Algorithm { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public AlgorithmCharacteristics Characteristics { get; init; } = new();
}

public record AlgorithmCharacteristics
{
    public string Speed { get; init; } = string.Empty;
    public string Quality { get; init; } = string.Empty;
    public string Complexity { get; init; } = string.Empty;
    public string BestFor { get; init; } = string.Empty;
}
