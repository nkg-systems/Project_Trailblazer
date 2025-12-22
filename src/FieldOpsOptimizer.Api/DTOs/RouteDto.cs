using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Api.DTOs;

public class RouteDto
{
    public Guid Id { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public string Name => RouteName; // Alias for API compatibility
    public Guid TechnicianId { get; set; }
    public TechnicianDto? Technician { get; set; }
    public DateTime RouteDate { get; set; }
    public RouteStatus Status { get; set; }
    public List<RouteStopDto> Stops { get; set; } = new();
    public double TotalDistance { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public TimeSpan? ActualDuration { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public CoordinateDto StartLocation { get; set; } = new();
    public CoordinateDto EndLocation { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

public class RouteStopDto
{
    public int StopOrder { get; set; }
    public Guid ServiceJobId { get; set; }
    public ServiceJobDto? ServiceJob { get; set; }
    public TimeSpan EstimatedArrivalTime { get; set; }
    public TimeSpan? ActualArrivalTime { get; set; }
    public TimeSpan? DepartureTime { get; set; }
    public double DistanceFromPrevious { get; set; }
    public TimeSpan TravelTimeFromPrevious { get; set; }
    public string? Notes { get; set; }
}

public class CreateRouteDto
{
    public string RouteName { get; set; } = string.Empty;
    public Guid TechnicianId { get; set; }
    public DateTime RouteDate { get; set; }
    public List<Guid> ServiceJobIds { get; set; } = new();
    public CoordinateDto? StartLocation { get; set; }
    public CoordinateDto? EndLocation { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

public class OptimizeRouteRequestDto
{
    public Guid TechnicianId { get; set; }
    public DateTime RouteDate { get; set; }
    public List<Guid> ServiceJobIds { get; set; } = new();
    public CoordinateDto? StartLocation { get; set; }
    public CoordinateDto? EndLocation { get; set; }
    public OptimizationAlgorithm Algorithm { get; set; } = OptimizationAlgorithm.NearestNeighbor;
    public OptimizationObjective Objective { get; set; } = OptimizationObjective.MinimizeDistance;
}

public class OptimizeRouteResponseDto
{
    public List<RouteStopDto> OptimizedStops { get; set; } = new();
    public double TotalDistance { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public decimal EstimatedCost { get; set; }
    public double OptimizationScore { get; set; }
    public string AlgorithmUsed { get; set; } = string.Empty;
    public TimeSpan OptimizationTime { get; set; }
}

public class BulkOptimizeRequestDto
{
    public DateTime RouteDate { get; set; }
    public List<Guid>? TechnicianIds { get; set; } // If null, optimize for all available technicians
    public OptimizationAlgorithm Algorithm { get; set; } = OptimizationAlgorithm.NearestNeighbor;
    public OptimizationObjective Objective { get; set; } = OptimizationObjective.MinimizeDistance;
    public bool IncludeTrafficData { get; set; } = true;
    public bool IncludeWeatherData { get; set; } = true;
}

public class BulkOptimizeResponseDto
{
    public List<RouteDto> OptimizedRoutes { get; set; } = new();
    public int TotalJobsScheduled { get; set; }
    public int UnassignedJobs { get; set; }
    public double TotalDistance { get; set; }
    public TimeSpan TotalOptimizationTime { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public enum OptimizationAlgorithm
{
    NearestNeighbor,
    TwoOpt,
    Genetic,
    SimulatedAnnealing
}

public enum OptimizationObjective
{
    MinimizeDistance,
    MinimizeTime,
    BalanceWorkload,
    MaximizeRevenue
}
