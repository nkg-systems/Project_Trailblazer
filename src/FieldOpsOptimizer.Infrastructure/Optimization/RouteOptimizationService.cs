using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AppInterfaces = FieldOpsOptimizer.Application.Common.Interfaces;
using DomainServices = FieldOpsOptimizer.Domain.Services;

namespace FieldOpsOptimizer.Infrastructure.Optimization;

public class RouteOptimizationService : IRouteOptimizationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RouteOptimizationService> _logger;

    public RouteOptimizationService(IServiceProvider serviceProvider, ILogger<RouteOptimizationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<AppInterfaces.RouteOptimizationResult> OptimizeRouteAsync(
        AppInterfaces.RouteOptimizationParameters parameters,
        OptimizationAlgorithm algorithm = OptimizationAlgorithm.TwoOpt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing route with {Algorithm} for {JobCount} jobs", 
            algorithm.ToString(), parameters.Jobs.Count());

        var optimizer = GetOptimizer(algorithm);
        if (optimizer == null)
        {
            throw new NotSupportedException($"Optimization algorithm {algorithm} is not supported");
        }

        if (!optimizer.SupportsObjective(parameters.Objective))
        {
            throw new NotSupportedException($"Algorithm {algorithm} does not support objective {parameters.Objective}");
        }

        // Convert application parameters to domain parameters
        var domainParams = ConvertToDomainParameters(parameters);
        var domainResult = await optimizer.OptimizeRouteAsync(domainParams, cancellationToken);
        
        _logger.LogInformation("Route optimization completed. Algorithm: {Algorithm}, " +
            "Distance: {Distance:F2}km, Duration: {Duration}, Cost: {Cost:C}",
            algorithm, domainResult.TotalDistanceKm, domainResult.TotalDuration, domainResult.TotalCost);

        // Convert domain result to application result
        return ConvertToApplicationResult(domainResult);
    }

    public async Task<AppInterfaces.AlgorithmComparisonResult> CompareAlgorithmsAsync(
        AppInterfaces.RouteOptimizationParameters parameters,
        IEnumerable<OptimizationAlgorithm> algorithms,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Comparing {AlgorithmCount} algorithms for {JobCount} jobs", 
            algorithms.Count(), parameters.Jobs.Count());

        var results = new List<AppInterfaces.RouteOptimizationResult>();
        var tasks = new List<Task<AppInterfaces.RouteOptimizationResult>>();

        var domainParams = ConvertToDomainParameters(parameters);
        
        foreach (var algorithm in algorithms)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var optimizer = GetOptimizer(algorithm);
            if (optimizer?.SupportsObjective(parameters.Objective) == true)
            {
                tasks.Add(OptimizeWithAlgorithmAsync(optimizer, domainParams, cancellationToken));
            }
        }

        var completedResults = await Task.WhenAll(tasks);
        results.AddRange(completedResults);

        // Find best result based on objective
        var bestResult = results.MinBy(r => GetObjectiveValue(r, parameters.Objective));

        return new AppInterfaces.AlgorithmComparisonResult
        {
            BestResult = bestResult!,
            AllResults = results.ToList(),
            ComparisonMetrics = BuildComparisonMetrics(results, parameters.Objective)
        };
    }

    public IEnumerable<OptimizationAlgorithm> GetAvailableAlgorithms(OptimizationObjective objective)
    {
        var algorithms = new[]
        {
            OptimizationAlgorithm.NearestNeighbor,
            OptimizationAlgorithm.TwoOpt,
            OptimizationAlgorithm.Genetic
        };

        return algorithms.Where(alg =>
        {
            var optimizer = GetOptimizer(alg);
            return optimizer?.SupportsObjective(objective) == true;
        });
    }

    public Task<TimeSpan> GetEstimatedTravelTimeAsync(
        Coordinate from, 
        Coordinate to, 
        CancellationToken cancellationToken = default)
    {
        // Simple estimation based on distance - in production would use routing service
        var distanceKm = from.DistanceToInKilometers(to);
        var averageSpeedKmh = 40.0; // Average city driving speed
        var hours = distanceKm / averageSpeedKmh;
        return Task.FromResult(TimeSpan.FromHours(hours));
    }

    public Task<double> GetEstimatedDistanceKmAsync(
        Coordinate from, 
        Coordinate to, 
        CancellationToken cancellationToken = default)
    {
        // Use the coordinate distance calculation
        return Task.FromResult(from.DistanceToInKilometers(to));
    }

    private IRouteOptimizer? GetOptimizer(OptimizationAlgorithm algorithm)
    {
        return algorithm switch
        {
            OptimizationAlgorithm.NearestNeighbor => _serviceProvider.GetService<NearestNeighborOptimizer>(),
            OptimizationAlgorithm.TwoOpt => _serviceProvider.GetService<TwoOptOptimizer>(),
            OptimizationAlgorithm.Genetic => _serviceProvider.GetService<GeneticOptimizer>(),
            _ => null
        };
    }

    private static double GetObjectiveValue(AppInterfaces.RouteOptimizationResult result, OptimizationObjective objective)
    {
        return objective switch
        {
            OptimizationObjective.MinimizeDistance => result.TotalDistanceKm,
            OptimizationObjective.MinimizeTime => result.TotalDuration.TotalMinutes,
            OptimizationObjective.MaximizeRevenue => -(double)result.TotalCost, // Negate for maximization
            _ => result.TotalDistanceKm
        };
    }

    private static Dictionary<string, object> BuildComparisonMetrics(
        List<AppInterfaces.RouteOptimizationResult> results,
        OptimizationObjective objective)
    {
        if (!results.Any()) return new Dictionary<string, object>();

        var objectiveValues = results.Select(r => GetObjectiveValue(r, objective)).ToArray();
        var optimizationTimes = results.Select(r => r.OptimizationTime.TotalMilliseconds).ToArray();

        return new Dictionary<string, object>
        {
            ["BestObjectiveValue"] = objectiveValues.Min(),
            ["WorstObjectiveValue"] = objectiveValues.Max(),
            ["AverageObjectiveValue"] = objectiveValues.Average(),
            ["FastestOptimizationMs"] = optimizationTimes.Min(),
            ["SlowestOptimizationMs"] = optimizationTimes.Max(),
            ["AverageOptimizationMs"] = optimizationTimes.Average(),
            ["ResultCount"] = results.Count
        };
    }

    private async Task<AppInterfaces.RouteOptimizationResult> OptimizeWithAlgorithmAsync(
        IRouteOptimizer optimizer, 
        DomainServices.RouteOptimizationParameters parameters, 
        CancellationToken cancellationToken)
    {
        var domainResult = await optimizer.OptimizeRouteAsync(parameters, cancellationToken);
        return ConvertToApplicationResult(domainResult);
    }

    private static DomainServices.RouteOptimizationParameters ConvertToDomainParameters(AppInterfaces.RouteOptimizationParameters appParams)
    {
        // Create a mock technician for now - in practice this would be retrieved from repository
        var mockTechnician = new Technician(
            "EMP001",
            "Mock",
            "Technician",
            "mock@example.com",
            "TenantId");

        return new DomainServices.RouteOptimizationParameters
        {
            Jobs = new List<ServiceJob>(),
            Technician = mockTechnician,
            StartLocation = new Coordinate(37.7749, -122.4194), // San Francisco
            EndLocation = new Coordinate(37.7849, -122.4094),
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(8),
            Constraints = new List<RouteConstraint>(),
            Objective = appParams.Objective
        };
    }

    private static AppInterfaces.RouteOptimizationResult ConvertToApplicationResult(DomainServices.RouteOptimizationResult domainResult)
    {
        return new AppInterfaces.RouteOptimizationResult
        {
            OptimizedStops = domainResult.OptimizedStops?.Select(stop => new AppInterfaces.OptimizedStop
            {
                Job = stop.Job,
                SequenceOrder = stop.SequenceOrder,
                DistanceFromPreviousKm = stop.DistanceFromPreviousKm,
                TravelTimeFromPrevious = stop.TravelTimeFromPrevious,
                EstimatedArrival = stop.EstimatedArrival,
                EstimatedDeparture = stop.EstimatedDeparture,
                HasConstraintViolations = stop.HasConstraintViolations,
                ConstraintViolations = stop.ConstraintViolations.ToList()
            }).ToList() ?? new List<AppInterfaces.OptimizedStop>(),
            TotalDistanceKm = domainResult.TotalDistanceKm,
            TotalDuration = domainResult.TotalDuration,
            TotalCost = domainResult.TotalCost,
            OptimizationTime = domainResult.OptimizationTime,
            Algorithm = domainResult.Algorithm,
            ConstraintViolations = domainResult.ConstraintViolations?.ToList() ?? new List<string>(),
            Metrics = new AppInterfaces.OptimizationMetrics
            {
                InitialCost = domainResult.Metrics?.InitialCost ?? 0,
                FinalCost = domainResult.Metrics?.FinalCost ?? 0,
                ImprovementPercentage = domainResult.Metrics?.ImprovementPercentage ?? 0,
                Evaluations = domainResult.Metrics?.Evaluations ?? 0
            },
            IsOptimal = domainResult.IsOptimal,
            Iterations = domainResult.Iterations
        };
    }
}
