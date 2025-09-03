using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;

namespace FieldOpsOptimizer.Application.Common.Interfaces;

/// <summary>
/// Service for route optimization operations
/// </summary>
public interface IRouteOptimizationService
{
    /// <summary>
    /// Optimizes a route for a technician with specified jobs
    /// </summary>
    Task<RouteOptimizationResult> OptimizeRouteAsync(
        RouteOptimizationParameters parameters,
        OptimizationAlgorithm algorithm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares multiple optimization algorithms for the same route
    /// </summary>
    Task<AlgorithmComparisonResult> CompareAlgorithmsAsync(
        RouteOptimizationParameters parameters,
        IEnumerable<OptimizationAlgorithm> algorithms,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available algorithms for a specific optimization objective
    /// </summary>
    IEnumerable<OptimizationAlgorithm> GetAvailableAlgorithms(OptimizationObjective objective);

    /// <summary>
    /// Calculates estimated travel time between two coordinates
    /// </summary>
    Task<TimeSpan> GetEstimatedTravelTimeAsync(
        Coordinate from,
        Coordinate to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates estimated distance between two coordinates
    /// </summary>
    Task<double> GetEstimatedDistanceKmAsync(
        Coordinate from,
        Coordinate to,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Parameters for route optimization
/// </summary>
public class RouteOptimizationParameters
{
    public required IEnumerable<ServiceJob> Jobs { get; init; }
    public required Technician Technician { get; init; }
    public OptimizationObjective Objective { get; init; } = OptimizationObjective.MinimizeDistance;
    public bool RespectTimeWindows { get; init; } = true;
    public bool ValidateSkills { get; init; } = true;
    public Coordinate? StartLocation { get; init; }
    public Coordinate? EndLocation { get; init; }
    public int MaxOptimizationTimeSeconds { get; init; } = 30;
}

/// <summary>
/// Result of route optimization
/// </summary>
public class RouteOptimizationResult
{
    public OptimizationAlgorithm Algorithm { get; set; }
    public double TotalDistanceKm { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public decimal TotalCost { get; set; }
    public TimeSpan OptimizationTime { get; set; }
    public bool IsOptimal { get; set; }
    public List<string> ConstraintViolations { get; set; } = new();
    public List<OptimizedStop> OptimizedStops { get; set; } = new();
    public OptimizationMetrics Metrics { get; set; } = new();
    public int Iterations { get; set; }
}

/// <summary>
/// Represents an optimized stop in a route
/// </summary>
public class OptimizedStop
{
    public required ServiceJob Job { get; set; }
    public int SequenceOrder { get; set; }
    public double DistanceFromPreviousKm { get; set; }
    public TimeSpan TravelTimeFromPrevious { get; set; }
    public DateTime EstimatedArrival { get; set; }
    public DateTime EstimatedDeparture { get; set; }
    public bool HasConstraintViolations { get; set; }
    public List<string> ConstraintViolations { get; set; } = new();
}

/// <summary>
/// Metrics from route optimization
/// </summary>
public class OptimizationMetrics
{
    public double InitialCost { get; set; }
    public double FinalCost { get; set; }
    public double ImprovementPercentage { get; set; }
    public int Evaluations { get; set; }
}

/// <summary>
/// Result of comparing multiple algorithms
/// </summary>
public class AlgorithmComparisonResult
{
    public RouteOptimizationResult BestResult { get; set; } = null!;
    public List<RouteOptimizationResult> AllResults { get; set; } = new();
    public Dictionary<string, object> ComparisonMetrics { get; set; } = new();
}
