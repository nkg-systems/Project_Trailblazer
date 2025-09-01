using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FieldOpsOptimizer.Infrastructure.Optimization;

/// <summary>
/// Service for orchestrating route optimization using different algorithms
/// </summary>
public interface IRouteOptimizationService
{
    /// <summary>
    /// Optimizes a route using the specified algorithm
    /// </summary>
    Task<RouteOptimizationResult> OptimizeRouteAsync(
        RouteOptimizationParameters parameters,
        OptimizationAlgorithm algorithm = OptimizationAlgorithm.TwoOpt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares multiple algorithms and returns the best result
    /// </summary>
    Task<RouteOptimizationComparison> CompareAlgorithmsAsync(
        RouteOptimizationParameters parameters,
        IEnumerable<OptimizationAlgorithm> algorithms,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available optimization algorithms for the given objective
    /// </summary>
    IEnumerable<OptimizationAlgorithm> GetAvailableAlgorithms(OptimizationObjective objective);
}

public class RouteOptimizationService : IRouteOptimizationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RouteOptimizationService> _logger;

    public RouteOptimizationService(IServiceProvider serviceProvider, ILogger<RouteOptimizationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<RouteOptimizationResult> OptimizeRouteAsync(
        RouteOptimizationParameters parameters,
        OptimizationAlgorithm algorithm = OptimizationAlgorithm.TwoOpt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing route with {Algorithm} for {JobCount} jobs", 
            algorithm, parameters.Jobs.Count);

        var optimizer = GetOptimizer(algorithm);
        if (optimizer == null)
        {
            throw new NotSupportedException($"Optimization algorithm {algorithm} is not supported");
        }

        if (!optimizer.SupportsObjective(parameters.Objective))
        {
            throw new NotSupportedException($"Algorithm {algorithm} does not support objective {parameters.Objective}");
        }

        var result = await optimizer.OptimizeRouteAsync(parameters, cancellationToken);
        
        _logger.LogInformation("Route optimization completed. Algorithm: {Algorithm}, " +
            "Distance: {Distance:F2}km, Duration: {Duration}, Cost: {Cost:C}",
            algorithm, result.TotalDistanceKm, result.TotalDuration, result.TotalCost);

        return result;
    }

    public async Task<RouteOptimizationComparison> CompareAlgorithmsAsync(
        RouteOptimizationParameters parameters,
        IEnumerable<OptimizationAlgorithm> algorithms,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Comparing {AlgorithmCount} algorithms for {JobCount} jobs", 
            algorithms.Count(), parameters.Jobs.Count);

        var results = new List<RouteOptimizationResult>();
        var tasks = new List<Task<RouteOptimizationResult>>();

        foreach (var algorithm in algorithms)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var optimizer = GetOptimizer(algorithm);
            if (optimizer?.SupportsObjective(parameters.Objective) == true)
            {
                tasks.Add(optimizer.OptimizeRouteAsync(parameters, cancellationToken));
            }
        }

        var completedResults = await Task.WhenAll(tasks);
        results.AddRange(completedResults);

        // Find best result based on objective
        var bestResult = results.MinBy(r => GetObjectiveValue(r, parameters.Objective));

        return new RouteOptimizationComparison
        {
            BestResult = bestResult!,
            AllResults = results,
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

    private static double GetObjectiveValue(RouteOptimizationResult result, OptimizationObjective objective)
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
        List<RouteOptimizationResult> results,
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
}

/// <summary>
/// Result of comparing multiple optimization algorithms
/// </summary>
public record RouteOptimizationComparison
{
    /// <summary>
    /// Best result among all algorithms tested
    /// </summary>
    public required RouteOptimizationResult BestResult { get; init; }

    /// <summary>
    /// Results from all algorithms
    /// </summary>
    public required IReadOnlyList<RouteOptimizationResult> AllResults { get; init; }

    /// <summary>
    /// Comparison metrics across algorithms
    /// </summary>
    public Dictionary<string, object> ComparisonMetrics { get; init; } = new();
}
