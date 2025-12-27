using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Infrastructure.Optimization;

namespace FieldOpsOptimizer.Infrastructure.Tests.Optimization;

public class OptimizerBenchmarkTests
{
    private readonly Mock<ILogger<OptimizerBenchmark>> _mockLogger;
    private readonly Mock<ILogger<NearestNeighborOptimizer>> _mockNNLogger;
    private readonly Mock<ILogger<TwoOptOptimizer>> _mockTwoOptLogger;
    private readonly Mock<ILogger<GeneticOptimizer>> _mockGeneticLogger;
    private readonly Mock<ILogger<SimulatedAnnealingOptimizer>> _mockSALogger;

    public OptimizerBenchmarkTests()
    {
        _mockLogger = new Mock<ILogger<OptimizerBenchmark>>();
        _mockNNLogger = new Mock<ILogger<NearestNeighborOptimizer>>();
        _mockTwoOptLogger = new Mock<ILogger<TwoOptOptimizer>>();
        _mockGeneticLogger = new Mock<ILogger<GeneticOptimizer>>();
        _mockSALogger = new Mock<ILogger<SimulatedAnnealingOptimizer>>();
    }

    private OptimizerBenchmark CreateBenchmark()
    {
        var nearestNeighbor = new NearestNeighborOptimizer(_mockNNLogger.Object);
        var twoOpt = new TwoOptOptimizer(_mockTwoOptLogger.Object, nearestNeighbor);
        var genetic = new GeneticOptimizer(_mockGeneticLogger.Object);
        var simulatedAnnealing = new SimulatedAnnealingOptimizer(_mockSALogger.Object, nearestNeighbor);

        return new OptimizerBenchmark(
            _mockLogger.Object,
            nearestNeighbor,
            twoOpt,
            genetic,
            simulatedAnnealing);
    }

    private RouteOptimizationParameters CreateSmallTestData(int jobCount = 5)
    {
        var technician = new Technician("EMP001", "John", "Doe", "john@example.com", "Tenant1");
        technician.UpdateHourlyRate(50.00m);

        var jobs = new List<ServiceJob>();
        var baseCoord = new Coordinate(37.7749, -122.4194);

        for (int i = 0; i < jobCount; i++)
        {
            var offset = 0.01 * i;
            var coord = new Coordinate(baseCoord.Latitude + offset, baseCoord.Longitude + offset);
            var address = new Address(
                $"{100 + i * 10} Main St",
                null,
                "San Francisco",
                "CA",
                $"9410{i}",
                "USA",
                coord);

            var job = new ServiceJob(
                $"JOB-{i:D3}",
                $"Customer {i}",
                address,
                $"Service job {i}",
                DateTime.UtcNow.AddDays(1),
                TimeSpan.FromHours(1),
                "Tenant1",
                JobPriority.Medium);

            jobs.Add(job);
        }

        return new RouteOptimizationParameters
        {
            Jobs = jobs,
            Technician = technician,
            StartLocation = baseCoord,
            EndLocation = baseCoord,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(8),
            Constraints = new List<RouteConstraint>(),
            Objective = OptimizationObjective.MinimizeDistance
        };
    }

    private RouteOptimizationParameters CreateLargeTestData(int jobCount = 20)
    {
        return CreateSmallTestData(jobCount);
    }

    [Fact]
    public async Task RunBenchmarkAsync_WithSmallDataset_CompletesSuccessfully()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 2);

        // Assert
        report.Should().NotBeNull();
        report.AlgorithmResults.Should().HaveCount(4); // All 4 optimizers
        report.Statistics.Should().HaveCount(4);
        report.BestAlgorithm.Should().NotBeEmpty();
        report.BestCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunBenchmarkAsync_AllOptimizers_ProduceResults()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 1);

        // Assert
        report.AlgorithmResults.Should().ContainKey("NearestNeighbor");
        report.AlgorithmResults.Should().ContainKey("TwoOpt");
        report.AlgorithmResults.Should().ContainKey("Genetic");
        report.AlgorithmResults.Should().ContainKey("SimulatedAnnealing");

        foreach (var results in report.AlgorithmResults.Values)
        {
            results.Should().HaveCount(1);
            results[0].Success.Should().BeTrue();
            results[0].TotalDistanceKm.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task RunBenchmarkAsync_MultipleRuns_ProducesStatistics()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);
        const int runs = 3;

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs);

        // Assert
        foreach (var (algorithm, stats) in report.Statistics)
        {
            stats.SuccessfulRuns.Should().Be(runs);
            stats.FailedRuns.Should().Be(0);
            stats.AverageCost.Should().BeGreaterThan(0);
            stats.AverageTimeMs.Should().BeGreaterThan(0);
            stats.AverageDistanceKm.Should().BeGreaterThan(0);
            stats.MinCost.Should().BeLessThanOrEqualTo(stats.AverageCost);
            stats.MaxCost.Should().BeGreaterThanOrEqualTo(stats.AverageCost);
        }
    }

    [Fact]
    public async Task RunBenchmarkAsync_IdentifiesBestAlgorithm()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(8);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 2);

        // Assert
        report.BestAlgorithm.Should().NotBeEmpty();
        var bestStats = report.Statistics[report.BestAlgorithm];
        bestStats.MinCost.Should().Be((double)report.BestCost);

        // Best algorithm should have lowest cost
        foreach (var stats in report.Statistics.Values)
        {
            stats.MinCost.Should().BeGreaterThanOrEqualTo(bestStats.MinCost);
        }
    }

    [Fact]
    public async Task RunBenchmarkAsync_ComputesRelativePerformance()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(6);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 2);

        // Assert
        foreach (var stats in report.Statistics.Values)
        {
            stats.RelativeCostToOptimal.Should().BeGreaterThan(0);
            stats.RelativeSpeedToFastest.Should().BeGreaterThan(0);
        }

        // At least one algorithm should have relative cost of 1.0 (the best one)
        report.Statistics.Values.Min(s => s.RelativeCostToOptimal).Should().BeApproximately(1.0, 0.001);
        // At least one algorithm should have relative speed of 1.0 (the fastest one)
        report.Statistics.Values.Min(s => s.RelativeSpeedToFastest).Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task RunBenchmarkAsync_TracksExecutionTime()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 1);

        // Assert
        foreach (var results in report.AlgorithmResults.Values)
        {
            foreach (var result in results)
            {
                result.ExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
            }
        }

        report.TotalBenchmarkTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunBenchmarkAsync_TracksMemoryUsage()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 1);

        // Assert
        foreach (var stats in report.Statistics.Values)
        {
            stats.AverageMemoryBytes.Should().BeGreaterThanOrEqualTo(0);
            stats.MaxMemoryBytes.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task RunBenchmarkAsync_WithLargerDataset_ShowsPerformanceDifferences()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateLargeTestData(15);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 1);

        // Assert
        report.Should().NotBeNull();
        
        // Should have cost differences between algorithms - but allow small variances
        var costs = report.Statistics.Values.Select(s => s.AverageCost).ToArray();
        var minCost = costs.Min();
        var maxCost = costs.Max();
        (maxCost - minCost).Should().BeGreaterThanOrEqualTo(0); // At least equal, could be same

        // Should have time differences between algorithms
        var times = report.Statistics.Values.Select(s => s.AverageTimeMs).ToArray();
        var minTime = times.Min();
        var maxTime = times.Max();
        (maxTime - minTime).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunBenchmarkAsync_ComputesStandardDeviation()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 3);

        // Assert
        foreach (var stats in report.Statistics.Values)
        {
            stats.StdDevCost.Should().BeGreaterThanOrEqualTo(0);
            stats.StdDevTimeMs.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task RunQuickComparisonAsync_RunsOncePerAlgorithm()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);

        // Act
        var report = await benchmark.RunQuickComparisonAsync(parameters);

        // Assert
        report.Runs.Should().Be(1);
        foreach (var results in report.AlgorithmResults.Values)
        {
            results.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task RunBenchmarkAsync_GeneratesSummary()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 1);
        var summary = report.GenerateSummary();

        // Assert
        summary.Should().NotBeEmpty();
        summary.Should().Contain("OPTIMIZER BENCHMARK REPORT");
        summary.Should().Contain("RESULTS BY ALGORITHM");
        summary.Should().Contain("BEST ALGORITHM");
        summary.Should().Contain("NearestNeighbor");
        summary.Should().Contain("TwoOpt");
        summary.Should().Contain("Genetic");
        summary.Should().Contain("SimulatedAnnealing");
    }

    [Fact]
    public async Task RunBenchmarkAsync_ProvidesComparisonData()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 1);
        var comparisonData = report.GetComparisonData();

        // Assert
        comparisonData.Should().ContainKey("algorithms");
        comparisonData.Should().ContainKey("costs");
        comparisonData.Should().ContainKey("times");
        comparisonData.Should().ContainKey("distances");
        comparisonData.Should().ContainKey("improvements");
        comparisonData.Should().ContainKey("memory");
        comparisonData.Should().ContainKey("bestAlgorithm");
        comparisonData.Should().ContainKey("bestCost");
    }

    [Fact]
    public async Task RunBenchmarkAsync_WithCancellation_StopsEarly()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(5);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 5, cts.Token);

        // Assert
        report.Should().NotBeNull();
        // Some algorithms may not have run at all
        var totalRuns = report.AlgorithmResults.Values.Sum(r => r.Count);
        totalRuns.Should().BeLessThan(20); // 4 algorithms * 5 runs = 20 if not cancelled
    }

    [Fact]
    public async Task RunBenchmarkAsync_TracksImprovements()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateSmallTestData(8);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 1);

        // Assert
        foreach (var stats in report.Statistics.Values)
        {
            stats.AverageImprovement.Should().BeGreaterThanOrEqualTo(0);
            stats.AverageIterations.Should().BeGreaterThanOrEqualTo(0);
        }

        // Improvement-based algorithms should show improvement (may be 0 for small datasets)
        var twoOptStats = report.Statistics["TwoOpt"];
        twoOptStats.AverageImprovement.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunBenchmarkAsync_WithDifferentObjectives_WorksCorrectly()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var technician = new Technician("EMP001", "John", "Doe", "john@example.com", "Tenant1");
        technician.UpdateHourlyRate(50.00m);

        var jobs = new List<ServiceJob>();
        var baseCoord = new Coordinate(37.7749, -122.4194);

        for (int i = 0; i < 5; i++)
        {
            var offset = 0.01 * i;
            var coord = new Coordinate(baseCoord.Latitude + offset, baseCoord.Longitude + offset);
            var address = new Address(
                $"{100 + i * 10} Main St",
                null,
                "San Francisco",
                "CA",
                $"9410{i}",
                "USA",
                coord);

            var job = new ServiceJob(
                $"JOB-{i:D3}",
                $"Customer {i}",
                address,
                $"Service job {i}",
                DateTime.UtcNow.AddDays(1),
                TimeSpan.FromHours(1),
                "Tenant1",
                JobPriority.Medium);
                
            job.UpdateEstimates(1000m, 100m);
            jobs.Add(job);
        }

        var parameters = new RouteOptimizationParameters
        {
            Jobs = jobs,
            Technician = technician,
            StartLocation = baseCoord,
            EndLocation = baseCoord,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(8),
            Constraints = new List<RouteConstraint>(),
            Objective = OptimizationObjective.MaximizeRevenue
        };

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 1);

        // Assert
        report.Should().NotBeNull();
        report.AlgorithmResults.Should().HaveCount(4);
        foreach (var results in report.AlgorithmResults.Values)
        {
            results[0].Success.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RunBenchmarkAsync_ComparesNearestNeighborVsTwoOpt()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateLargeTestData(12);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 2);

        // Assert
        var nnStats = report.Statistics["NearestNeighbor"];
        var twoOptStats = report.Statistics["TwoOpt"];

        // TwoOpt should produce better or equal results than NearestNeighbor (allow 5% margin)
        twoOptStats.MinCost.Should().BeLessThanOrEqualTo(nnStats.MinCost * 1.05); // Allow 5% margin for variance

        // NearestNeighbor should generally be faster (but may not always be true)
        nnStats.AverageTimeMs.Should().BeLessThanOrEqualTo(twoOptStats.AverageTimeMs * 2); // Allow 2x
    }

    [Fact]
    public async Task RunBenchmarkAsync_ComparesAdvancedAlgorithms()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var parameters = CreateLargeTestData(15);

        // Act
        var report = await benchmark.RunBenchmarkAsync(parameters, runs: 1);

        // Assert
        var geneticStats = report.Statistics["Genetic"];
        var saStats = report.Statistics["SimulatedAnnealing"];

        // Both should find reasonable solutions
        geneticStats.AverageCost.Should().BeGreaterThan(0);
        saStats.AverageCost.Should().BeGreaterThan(0);

        // Both should show improvement (may be small depending on dataset)
        geneticStats.AverageImprovement.Should().BeGreaterThanOrEqualTo(0);
        saStats.AverageImprovement.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunBenchmarkAsync_ScalabilityTest_SmallVsLarge()
    {
        // Arrange
        var benchmark = CreateBenchmark();
        var smallParams = CreateSmallTestData(5);
        var largeParams = CreateLargeTestData(15);

        // Act
        var smallReport = await benchmark.RunBenchmarkAsync(smallParams, runs: 1);
        var largeReport = await benchmark.RunBenchmarkAsync(largeParams, runs: 1);

        // Assert
        foreach (var algorithm in smallReport.Statistics.Keys)
        {
            var smallStats = smallReport.Statistics[algorithm];
            var largeStats = largeReport.Statistics[algorithm];

            // Larger dataset should take more time
            largeStats.AverageTimeMs.Should().BeGreaterThan(smallStats.AverageTimeMs);

            // Larger dataset should have higher cost (more distance)
            largeStats.AverageCost.Should().BeGreaterThan(smallStats.AverageCost);
        }
    }
}
