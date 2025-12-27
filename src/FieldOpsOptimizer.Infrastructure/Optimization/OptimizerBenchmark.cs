using System.Diagnostics;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FieldOpsOptimizer.Infrastructure.Optimization;

/// <summary>
/// Benchmarking framework for comparing route optimization algorithms
/// </summary>
public class OptimizerBenchmark
{
    private readonly ILogger<OptimizerBenchmark> _logger;
    private readonly NearestNeighborOptimizer _nearestNeighbor;
    private readonly TwoOptOptimizer _twoOpt;
    private readonly GeneticOptimizer _genetic;
    private readonly SimulatedAnnealingOptimizer _simulatedAnnealing;

    public OptimizerBenchmark(
        ILogger<OptimizerBenchmark> logger,
        NearestNeighborOptimizer nearestNeighbor,
        TwoOptOptimizer twoOpt,
        GeneticOptimizer genetic,
        SimulatedAnnealingOptimizer simulatedAnnealing)
    {
        _logger = logger;
        _nearestNeighbor = nearestNeighbor;
        _twoOpt = twoOpt;
        _genetic = genetic;
        _simulatedAnnealing = simulatedAnnealing;
    }

    /// <summary>
    /// Run a comprehensive benchmark comparing all optimizers
    /// </summary>
    public async Task<BenchmarkReport> RunBenchmarkAsync(
        RouteOptimizationParameters parameters,
        int runs = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting optimizer benchmark with {JobCount} jobs, {Runs} runs per algorithm",
            parameters.Jobs.Count, runs);

        var optimizers = new Dictionary<string, IRouteOptimizer>
        {
            ["NearestNeighbor"] = _nearestNeighbor,
            ["TwoOpt"] = _twoOpt,
            ["Genetic"] = _genetic,
            ["SimulatedAnnealing"] = _simulatedAnnealing
        };

        var algorithmResults = new Dictionary<string, List<BenchmarkResult>>();

        foreach (var (name, optimizer) in optimizers)
        {
            if (!optimizer.SupportsObjective(parameters.Objective))
            {
                _logger.LogWarning("Optimizer {Name} does not support objective {Objective}, skipping",
                    name, parameters.Objective);
                continue;
            }

            var results = new List<BenchmarkResult>();

            for (int i = 0; i < runs; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                _logger.LogDebug("Running {Optimizer} - iteration {Iteration}/{Total}",
                    name, i + 1, runs);

                var result = await BenchmarkSingleRunAsync(optimizer, name, parameters, cancellationToken);
                results.Add(result);

                // Small delay between runs to reduce system load variance
                await Task.Delay(100, cancellationToken);
            }

            algorithmResults[name] = results;
        }

        var report = new BenchmarkReport
        {
            Parameters = parameters,
            Runs = runs,
            AlgorithmResults = algorithmResults,
            TotalBenchmarkTime = algorithmResults.Values.SelectMany(r => r).Sum(r => r.ExecutionTime.TotalMilliseconds)
        };

        ComputeStatistics(report);

        _logger.LogInformation("Benchmark completed. Best algorithm: {Best} with cost {Cost:F2}",
            report.BestAlgorithm, report.BestCost);

        return report;
    }

    /// <summary>
    /// Run a quick comparison benchmark (single run per optimizer)
    /// </summary>
    public async Task<BenchmarkReport> RunQuickComparisonAsync(
        RouteOptimizationParameters parameters,
        CancellationToken cancellationToken = default)
    {
        return await RunBenchmarkAsync(parameters, runs: 1, cancellationToken);
    }

    /// <summary>
    /// Benchmark a single optimizer run
    /// </summary>
    private async Task<BenchmarkResult> BenchmarkSingleRunAsync(
        IRouteOptimizer optimizer,
        string algorithmName,
        RouteOptimizationParameters parameters,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        RouteOptimizationResult? result = null;
        Exception? error = null;

        try
        {
            result = await optimizer.OptimizeRouteAsync(parameters, cancellationToken);
        }
        catch (Exception ex)
        {
            error = ex;
            _logger.LogError(ex, "Error running optimizer {Algorithm}", algorithmName);
        }

        stopwatch.Stop();
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var memoryUsed = Math.Max(0, memoryAfter - memoryBefore);

        return new BenchmarkResult
        {
            AlgorithmName = algorithmName,
            ExecutionTime = stopwatch.Elapsed,
            MemoryUsedBytes = memoryUsed,
            TotalCost = result?.TotalCost ?? 0,
            TotalDistanceKm = result?.TotalDistanceKm ?? 0,
            TotalDuration = result?.TotalDuration ?? TimeSpan.Zero,
            Iterations = result?.Iterations ?? 0,
            Metrics = result?.Metrics,
            ConstraintViolations = result?.ConstraintViolations?.Count ?? 0,
            Success = error == null,
            ErrorMessage = error?.Message
        };
    }

    /// <summary>
    /// Compute statistical summaries for the benchmark report
    /// </summary>
    private void ComputeStatistics(BenchmarkReport report)
    {
        foreach (var (algorithm, results) in report.AlgorithmResults)
        {
            var successfulRuns = results.Where(r => r.Success).ToList();
            if (!successfulRuns.Any())
                continue;

            var costs = successfulRuns.Select(r => (double)r.TotalCost).ToArray();
            var times = successfulRuns.Select(r => r.ExecutionTime.TotalMilliseconds).ToArray();
            var distances = successfulRuns.Select(r => r.TotalDistanceKm).ToArray();
            var memory = successfulRuns.Select(r => r.MemoryUsedBytes).ToArray();

            var stats = new AlgorithmStatistics
            {
                AlgorithmName = algorithm,
                SuccessfulRuns = successfulRuns.Count,
                FailedRuns = results.Count - successfulRuns.Count,

                // Cost statistics
                AverageCost = costs.Average(),
                MinCost = costs.Min(),
                MaxCost = costs.Max(),
                StdDevCost = CalculateStandardDeviation(costs),

                // Time statistics
                AverageTimeMs = times.Average(),
                MinTimeMs = times.Min(),
                MaxTimeMs = times.Max(),
                StdDevTimeMs = CalculateStandardDeviation(times),

                // Distance statistics
                AverageDistanceKm = distances.Average(),
                MinDistanceKm = distances.Min(),
                MaxDistanceKm = distances.Max(),

                // Memory statistics
                AverageMemoryBytes = (long)memory.Average(),
                MaxMemoryBytes = memory.Max(),

                // Improvement statistics
                AverageImprovement = successfulRuns.Average(r => r.Metrics?.ImprovementPercentage ?? 0),
                AverageIterations = successfulRuns.Average(r => r.Iterations)
            };

            report.Statistics[algorithm] = stats;
        }

        // Determine best algorithm based on objective
        var bestStats = report.Statistics.Values.MinBy(s => s.AverageCost);
        if (bestStats != null)
        {
            report.BestAlgorithm = bestStats.AlgorithmName;
            report.BestCost = (decimal)bestStats.MinCost;
        }

        // Compute relative performance (normalized to best)
        if (bestStats != null)
        {
            foreach (var stats in report.Statistics.Values)
            {
                stats.RelativeCostToOptimal = stats.AverageCost / bestStats.MinCost;
                stats.RelativeSpeedToFastest = stats.AverageTimeMs / report.Statistics.Values.Min(s => s.AverageTimeMs);
            }
        }
    }

    /// <summary>
    /// Calculate standard deviation
    /// </summary>
    private static double CalculateStandardDeviation(double[] values)
    {
        if (values.Length < 2)
            return 0;

        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / (values.Length - 1));
    }
}

/// <summary>
/// Benchmark report containing results from all algorithms
/// </summary>
public class BenchmarkReport
{
    public required RouteOptimizationParameters Parameters { get; init; }
    public int Runs { get; init; }
    public Dictionary<string, List<BenchmarkResult>> AlgorithmResults { get; init; } = new();
    public Dictionary<string, AlgorithmStatistics> Statistics { get; init; } = new();
    public double TotalBenchmarkTime { get; init; }
    public string BestAlgorithm { get; set; } = string.Empty;
    public decimal BestCost { get; set; }

    /// <summary>
    /// Generate a formatted summary of the benchmark results
    /// </summary>
    public string GenerateSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine("=== OPTIMIZER BENCHMARK REPORT ===");
        summary.AppendLine($"Jobs: {Parameters.Jobs.Count}");
        summary.AppendLine($"Objective: {Parameters.Objective}");
        summary.AppendLine($"Runs per algorithm: {Runs}");
        summary.AppendLine($"Total benchmark time: {TotalBenchmarkTime:F0}ms");
        summary.AppendLine();

        summary.AppendLine("=== RESULTS BY ALGORITHM ===");
        foreach (var (algorithm, stats) in Statistics.OrderBy(s => s.Value.AverageCost))
        {
            summary.AppendLine($"\n{algorithm}:");
            summary.AppendLine($"  Success rate: {stats.SuccessfulRuns}/{stats.SuccessfulRuns + stats.FailedRuns}");
            summary.AppendLine($"  Cost: {stats.AverageCost:F2} ± {stats.StdDevCost:F2} (min: {stats.MinCost:F2}, max: {stats.MaxCost:F2})");
            summary.AppendLine($"  Time: {stats.AverageTimeMs:F1}ms ± {stats.StdDevTimeMs:F1}ms");
            summary.AppendLine($"  Distance: {stats.AverageDistanceKm:F2}km");
            summary.AppendLine($"  Improvement: {stats.AverageImprovement:F1}%");
            summary.AppendLine($"  Memory: {stats.AverageMemoryBytes / 1024:F0}KB (peak: {stats.MaxMemoryBytes / 1024:F0}KB)");
            summary.AppendLine($"  Relative cost: {stats.RelativeCostToOptimal:F2}x optimal");
            summary.AppendLine($"  Relative speed: {stats.RelativeSpeedToFastest:F2}x fastest");
        }

        summary.AppendLine($"\n=== BEST ALGORITHM: {BestAlgorithm} ===");

        return summary.ToString();
    }

    /// <summary>
    /// Get comparison data for visualization/analysis
    /// </summary>
    public Dictionary<string, object> GetComparisonData()
    {
        return new Dictionary<string, object>
        {
            ["algorithms"] = Statistics.Keys.ToList(),
            ["costs"] = Statistics.Values.Select(s => s.AverageCost).ToList(),
            ["times"] = Statistics.Values.Select(s => s.AverageTimeMs).ToList(),
            ["distances"] = Statistics.Values.Select(s => s.AverageDistanceKm).ToList(),
            ["improvements"] = Statistics.Values.Select(s => s.AverageImprovement).ToList(),
            ["memory"] = Statistics.Values.Select(s => s.AverageMemoryBytes).ToList(),
            ["bestAlgorithm"] = BestAlgorithm,
            ["bestCost"] = BestCost
        };
    }
}

/// <summary>
/// Result from a single benchmark run
/// </summary>
public class BenchmarkResult
{
    public required string AlgorithmName { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public long MemoryUsedBytes { get; init; }
    public decimal TotalCost { get; init; }
    public double TotalDistanceKm { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public int Iterations { get; init; }
    public OptimizationMetrics? Metrics { get; init; }
    public int ConstraintViolations { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Statistical summary for an algorithm across multiple runs
/// </summary>
public class AlgorithmStatistics
{
    public required string AlgorithmName { get; init; }
    public int SuccessfulRuns { get; init; }
    public int FailedRuns { get; init; }

    // Cost statistics
    public double AverageCost { get; init; }
    public double MinCost { get; init; }
    public double MaxCost { get; init; }
    public double StdDevCost { get; init; }

    // Time statistics
    public double AverageTimeMs { get; init; }
    public double MinTimeMs { get; init; }
    public double MaxTimeMs { get; init; }
    public double StdDevTimeMs { get; init; }

    // Distance statistics
    public double AverageDistanceKm { get; init; }
    public double MinDistanceKm { get; init; }
    public double MaxDistanceKm { get; init; }

    // Memory statistics
    public long AverageMemoryBytes { get; init; }
    public long MaxMemoryBytes { get; init; }

    // Performance metrics
    public double AverageImprovement { get; init; }
    public double AverageIterations { get; init; }

    // Relative performance
    public double RelativeCostToOptimal { get; set; }
    public double RelativeSpeedToFastest { get; set; }
}
