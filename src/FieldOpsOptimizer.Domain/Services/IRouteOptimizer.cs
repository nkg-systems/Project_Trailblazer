using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;

namespace FieldOpsOptimizer.Domain.Services;

/// <summary>
/// Interface for route optimization algorithms
/// </summary>
public interface IRouteOptimizer
{
    /// <summary>
    /// Optimizes a route by reordering service jobs
    /// </summary>
    /// <param name="parameters">Optimization parameters including jobs, constraints, and objectives</param>
    /// <param name="cancellationToken">Cancellation token for long-running operations</param>
    /// <returns>Optimization result with the optimized route and performance metrics</returns>
    Task<RouteOptimizationResult> OptimizeRouteAsync(
        RouteOptimizationParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The optimization algorithm type
    /// </summary>
    OptimizationAlgorithm Algorithm { get; }

    /// <summary>
    /// Whether this optimizer supports the given optimization objective
    /// </summary>
    bool SupportsObjective(OptimizationObjective objective);
}

/// <summary>
/// Parameters for route optimization
/// </summary>
public record RouteOptimizationParameters
{
    /// <summary>
    /// Service jobs to be included in the route
    /// </summary>
    public required IReadOnlyList<ServiceJob> Jobs { get; init; }

    /// <summary>
    /// The technician assigned to this route
    /// </summary>
    public required Technician Technician { get; init; }

    /// <summary>
    /// Optimization objective (minimize distance, time, or cost)
    /// </summary>
    public OptimizationObjective Objective { get; init; } = OptimizationObjective.MinimizeDistance;

    /// <summary>
    /// Maximum optimization time in seconds
    /// </summary>
    public int MaxOptimizationTimeSeconds { get; init; } = 30;

    /// <summary>
    /// Whether to respect job time windows
    /// </summary>
    public bool RespectTimeWindows { get; init; } = true;

    /// <summary>
    /// Whether to validate technician skills match job requirements
    /// </summary>
    public bool ValidateSkills { get; init; } = true;

    /// <summary>
    /// Starting location for the route (technician's home or depot)
    /// </summary>
    public Coordinate? StartLocation { get; init; }

    /// <summary>
    /// Ending location for the route (typically same as start)
    /// </summary>
    public Coordinate? EndLocation { get; init; }

    /// <summary>
    /// Custom distance/time matrix if available
    /// </summary>
    public DistanceMatrix? DistanceMatrix { get; init; }

    /// <summary>
    /// Technician ID for compatibility with application layer
    /// </summary>
    public Guid TechnicianId => Technician.Id;

    /// <summary>
    /// Route start time
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Route end time
    /// </summary>
    public DateTime EndTime { get; init; } = DateTime.UtcNow.AddHours(8);

    /// <summary>
    /// Additional route constraints to apply
    /// </summary>
    public IReadOnlyList<RouteConstraint> Constraints { get; init; } = new List<RouteConstraint>();
}

/// <summary>
/// Result of route optimization
/// </summary>
public record RouteOptimizationResult
{
    /// <summary>
    /// Optimized sequence of route stops
    /// </summary>
    public required IReadOnlyList<OptimizedRouteStop> OptimizedStops { get; init; }

    /// <summary>
    /// Total route distance in kilometers
    /// </summary>
    public double TotalDistanceKm { get; init; }

    /// <summary>
    /// Total route duration including travel and service time
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Total estimated cost of the route
    /// </summary>
    public decimal TotalCost { get; init; }

    /// <summary>
    /// Optimization algorithm used
    /// </summary>
    public OptimizationAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Time taken to compute the optimization
    /// </summary>
    public TimeSpan OptimizationTime { get; init; }

    /// <summary>
    /// Number of iterations performed (for iterative algorithms)
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// Whether the optimization completed successfully
    /// </summary>
    public bool IsOptimal { get; init; }

    /// <summary>
    /// Any constraint violations found
    /// </summary>
    public IReadOnlyList<string> ConstraintViolations { get; init; } = new List<string>();

    /// <summary>
    /// Performance metrics and debug information
    /// </summary>
    public OptimizationMetrics Metrics { get; init; } = new();

    /// <summary>
    /// Optimized route for compatibility with application layer
    /// </summary>
    public IReadOnlyList<ServiceJob> OptimizedRoute => OptimizedStops.Select(s => s.Job).ToList();

    /// <summary>
    /// Algorithm used for compatibility with application layer
    /// </summary>
    public OptimizationAlgorithm AlgorithmUsed => Algorithm;

    /// <summary>
    /// Constraints violated for compatibility with application layer
    /// </summary>
    public IReadOnlyList<string> ConstraintsViolated => ConstraintViolations;

    /// <summary>
    /// Performance metrics for compatibility with application layer
    /// </summary>
    public Dictionary<string, object> PerformanceMetrics => Metrics.AdditionalMetrics;
}

/// <summary>
/// Optimized route stop with calculated timing and distance
/// </summary>
public record OptimizedRouteStop
{
    /// <summary>
    /// The service job for this stop
    /// </summary>
    public required ServiceJob Job { get; init; }

    /// <summary>
    /// Sequence order in the optimized route
    /// </summary>
    public int SequenceOrder { get; init; }

    /// <summary>
    /// Distance from previous stop in kilometers
    /// </summary>
    public double DistanceFromPreviousKm { get; init; }

    /// <summary>
    /// Travel time from previous stop
    /// </summary>
    public TimeSpan TravelTimeFromPrevious { get; init; }

    /// <summary>
    /// Estimated arrival time at this stop
    /// </summary>
    public DateTime EstimatedArrival { get; init; }

    /// <summary>
    /// Estimated departure time from this stop
    /// </summary>
    public DateTime EstimatedDeparture { get; init; }

    /// <summary>
    /// Whether this stop violates any constraints
    /// </summary>
    public bool HasConstraintViolations { get; init; }

    /// <summary>
    /// List of constraint violations for this stop
    /// </summary>
    public IReadOnlyList<string> ConstraintViolations { get; init; } = new List<string>();

    // Compatibility properties for application layer
    /// <summary>
    /// Job ID for compatibility with application layer
    /// </summary>
    public Guid JobId => Job.Id;

    /// <summary>
    /// Job location for compatibility with application layer
    /// </summary>
    public Coordinate Location => Job.ServiceAddress?.Coordinate ?? new Coordinate(0, 0);

    /// <summary>
    /// Arrival time for compatibility with application layer
    /// </summary>
    public DateTime ArrivalTime => EstimatedArrival;

    /// <summary>
    /// Departure time for compatibility with application layer
    /// </summary>
    public DateTime DepartureTime => EstimatedDeparture;

    /// <summary>
    /// Estimated duration in minutes for compatibility with application layer
    /// </summary>
    public int EstimatedDurationMinutes => (int)(EstimatedDeparture - EstimatedArrival).TotalMinutes;

    /// <summary>
    /// Sequence number for compatibility with application layer
    /// </summary>
    public int SequenceNumber => SequenceOrder;
}

/// <summary>
/// Distance and time matrix between locations
/// </summary>
public record DistanceMatrix
{
    /// <summary>
    /// Locations included in the matrix
    /// </summary>
    public required IReadOnlyList<Coordinate> Locations { get; init; }

    /// <summary>
    /// Distance matrix in kilometers [from][to]
    /// </summary>
    public required double[,] Distances { get; init; }

    /// <summary>
    /// Duration matrix in seconds [from][to]
    /// </summary>
    public required int[,] Durations { get; init; }

    /// <summary>
    /// Gets distance between two location indices
    /// </summary>
    public double GetDistance(int fromIndex, int toIndex) => Distances[fromIndex, toIndex];

    /// <summary>
    /// Gets duration between two location indices
    /// </summary>
    public TimeSpan GetDuration(int fromIndex, int toIndex) => TimeSpan.FromSeconds(Durations[fromIndex, toIndex]);
}

/// <summary>
/// Performance metrics for optimization algorithms
/// </summary>
public record OptimizationMetrics
{
    /// <summary>
    /// Initial route cost before optimization
    /// </summary>
    public double InitialCost { get; init; }

    /// <summary>
    /// Final route cost after optimization
    /// </summary>
    public double FinalCost { get; init; }

    /// <summary>
    /// Improvement percentage
    /// </summary>
    public double ImprovementPercentage => InitialCost > 0 ? (InitialCost - FinalCost) / InitialCost * 100 : 0;

    /// <summary>
    /// Number of evaluations performed
    /// </summary>
    public int Evaluations { get; init; }

    /// <summary>
    /// Best cost found in each iteration (for tracking convergence)
    /// </summary>
    public IReadOnlyList<double> CostHistory { get; init; } = new List<double>();

    /// <summary>
    /// Additional algorithm-specific metrics
    /// </summary>
    public Dictionary<string, object> AdditionalMetrics { get; init; } = new();
}
