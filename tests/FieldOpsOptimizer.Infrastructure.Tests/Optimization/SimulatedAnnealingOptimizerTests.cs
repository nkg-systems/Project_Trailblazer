using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Infrastructure.Optimization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace FieldOpsOptimizer.Infrastructure.Tests.Optimization;

public class SimulatedAnnealingOptimizerTests
{
    private readonly Mock<ILogger<SimulatedAnnealingOptimizer>> _mockLogger;
    private readonly Mock<ILogger<NearestNeighborOptimizer>> _mockNNLogger;
    private readonly NearestNeighborOptimizer _nearestNeighborOptimizer;
    private readonly SimulatedAnnealingOptimizer _optimizer;

    public SimulatedAnnealingOptimizerTests()
    {
        _mockLogger = new Mock<ILogger<SimulatedAnnealingOptimizer>>();
        _mockNNLogger = new Mock<ILogger<NearestNeighborOptimizer>>();
        _nearestNeighborOptimizer = new NearestNeighborOptimizer(_mockNNLogger.Object);
        _optimizer = new SimulatedAnnealingOptimizer(_mockLogger.Object, _nearestNeighborOptimizer);
    }

    [Fact]
    public void Algorithm_ShouldReturnSimulatedAnnealing()
    {
        // Act
        var algorithm = _optimizer.Algorithm;

        // Assert
        algorithm.Should().Be(OptimizationAlgorithm.SimulatedAnnealing);
    }

    [Fact]
    public void SupportsObjective_WithMinimizeDistance_ShouldReturnTrue()
    {
        // Act
        var supports = _optimizer.SupportsObjective(OptimizationObjective.MinimizeDistance);

        // Assert
        supports.Should().BeTrue();
    }

    [Fact]
    public void SupportsObjective_WithMinimizeTime_ShouldReturnTrue()
    {
        // Act
        var supports = _optimizer.SupportsObjective(OptimizationObjective.MinimizeTime);

        // Assert
        supports.Should().BeTrue();
    }

    [Fact]
    public void SupportsObjective_WithMaximizeRevenue_ShouldReturnTrue()
    {
        // Act
        var supports = _optimizer.SupportsObjective(OptimizationObjective.MaximizeRevenue);

        // Assert
        supports.Should().BeTrue();
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithEmptyJobs_ShouldReturnEmptyResult()
    {
        // Arrange
        var parameters = CreateBasicParameters(new List<ServiceJob>());

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().BeEmpty();
        result.TotalDistanceKm.Should().Be(0);
        // Algorithm is NearestNeighbor since SA returns early from NN initializer with empty jobs
        result.Algorithm.Should().Be(OptimizationAlgorithm.NearestNeighbor);
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithSingleJob_ShouldReturnSingleStop()
    {
        // Arrange
        var job = CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060));
        var parameters = CreateBasicParameters(new List<ServiceJob> { job });

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(1);
        result.Algorithm.Should().Be(OptimizationAlgorithm.SimulatedAnnealing);
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithMultipleJobs_ShouldOptimizeRoute()
    {
        // Arrange
        var jobs = new List<ServiceJob>
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)), // NYC
            CreateTestJob("JOB002", new Coordinate(34.0522, -118.2437)), // LA
            CreateTestJob("JOB003", new Coordinate(41.8781, -87.6298))  // Chicago
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(3);
        result.TotalDistanceKm.Should().BeGreaterThan(0);
        result.Algorithm.Should().Be(OptimizationAlgorithm.SimulatedAnnealing);
        result.Metrics.Should().NotBeNull();
        result.Metrics.Evaluations.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldImproveOverInitialSolution()
    {
        // Arrange
        var jobs = new List<ServiceJob>
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),  // NYC
            CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851)),  // Queens (near NYC)
            CreateTestJob("JOB003", new Coordinate(34.0522, -118.2437)), // LA (far away)
            CreateTestJob("JOB004", new Coordinate(40.7306, -73.9352))   // Brooklyn (near NYC)
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(4);
        result.Metrics.InitialCost.Should().BeGreaterThan(0);
        result.Metrics.FinalCost.Should().BeLessThanOrEqualTo(result.Metrics.InitialCost);
        result.Metrics.ImprovementPercentage.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithMinimizeDistanceObjective_ShouldMinimizeDistance()
    {
        // Arrange
        var jobs = new List<ServiceJob>
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(34.0522, -118.2437)),
            CreateTestJob("JOB003", new Coordinate(41.8781, -87.6298))
        };
        var parameters = CreateBasicParametersWithObjective(jobs, OptimizationObjective.MinimizeDistance);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.TotalDistanceKm.Should().BeGreaterThan(0);
        result.Metrics.FinalCost.Should().BeLessThanOrEqualTo(result.Metrics.InitialCost);
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var jobs = new List<ServiceJob>();
        for (int i = 0; i < 20; i++)
        {
            jobs.Add(CreateTestJob($"JOB{i:D3}", new Coordinate(40.0 + i * 0.1, -74.0 + i * 0.1)));
        }
        var parameters = CreateBasicParameters(jobs);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(10)); // Cancel quickly

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters, cts.Token);

        // Assert - Should complete or respect cancellation gracefully
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithSkillValidation_ShouldFilterJobs()
    {
        // Arrange
        var job1 = CreateTestJobWithSkills("JOB001", new Coordinate(40.7128, -74.0060), new[] { "Plumbing" });
        var job2 = CreateTestJobWithSkills("JOB002", new Coordinate(34.0522, -118.2437), new[] { "Electrical" });
        var job3 = CreateTestJobWithSkills("JOB003", new Coordinate(41.8781, -87.6298), new[] { "Plumbing" });
        
        var jobs = new List<ServiceJob> { job1, job2, job3 };
        var technician = CreateTechnicianWithSkills(new[] { "Plumbing", "HVAC" });
        var parameters = CreateParametersWithSkillValidation(jobs, technician);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(2); // Only 2 jobs match skills
        result.OptimizedStops.Should().Contain(s => s.Job.JobNumber == "JOB001");
        result.OptimizedStops.Should().Contain(s => s.Job.JobNumber == "JOB003");
        result.OptimizedStops.Should().NotContain(s => s.Job.JobNumber == "JOB002");
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldIncludeMetrics()
    {
        // Arrange
        var jobs = new List<ServiceJob>
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(34.0522, -118.2437)),
            CreateTestJob("JOB003", new Coordinate(41.8781, -87.6298))
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Metrics.Should().NotBeNull();
        result.Metrics.InitialCost.Should().BeGreaterThan(0);
        result.Metrics.FinalCost.Should().BeGreaterThan(0);
        result.Metrics.Evaluations.Should().BeGreaterThan(0);
        result.Metrics.AdditionalMetrics.Should().NotBeNull();
        result.Metrics.AdditionalMetrics.Should().ContainKey("InitialTemperature");
        result.Metrics.AdditionalMetrics.Should().ContainKey("FinalTemperature");
        result.Metrics.AdditionalMetrics.Should().ContainKey("CoolingRate");
        result.Metrics.AdditionalMetrics.Should().ContainKey("AcceptedMoves");
        result.Metrics.AdditionalMetrics.Should().ContainKey("RejectedMoves");
        result.Metrics.AdditionalMetrics.Should().ContainKey("AcceptanceRate");
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldHaveReasonableAcceptanceRate()
    {
        // Arrange
        var jobs = new List<ServiceJob>();
        for (int i = 0; i < 10; i++)
        {
            jobs.Add(CreateTestJob($"JOB{i:D3}", new Coordinate(40.0 + i * 0.5, -74.0 + i * 0.5)));
        }
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Metrics.AdditionalMetrics.Should().ContainKey("AcceptanceRate");
        var acceptanceRate = (double)result.Metrics.AdditionalMetrics["AcceptanceRate"];
        acceptanceRate.Should().BeGreaterThan(0);
        acceptanceRate.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldTrackCostHistory()
    {
        // Arrange
        var jobs = new List<ServiceJob>
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(34.0522, -118.2437)),
            CreateTestJob("JOB003", new Coordinate(41.8781, -87.6298)),
            CreateTestJob("JOB004", new Coordinate(42.3601, -71.0589))
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Metrics.CostHistory.Should().NotBeNull();
        result.Metrics.CostHistory.Should().NotBeEmpty();
        result.Metrics.CostHistory.First().Should().Be(result.Metrics.InitialCost);
        result.Metrics.CostHistory.Last().Should().Be(result.Metrics.FinalCost);
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithLargeNumberOfJobs_ShouldComplete()
    {
        // Arrange
        var jobs = new List<ServiceJob>();
        for (int i = 0; i < 15; i++)
        {
            jobs.Add(CreateTestJob($"JOB{i:D3}", new Coordinate(40.0 + i * 0.2, -74.0 + i * 0.2)));
        }
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(15);
        result.TotalDistanceKm.Should().BeGreaterThan(0);
        result.OptimizationTime.Should().BeLessThan(TimeSpan.FromSeconds(30)); // Should be reasonable
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithMaximizeRevenueObjective_ShouldConsiderRevenue()
    {
        // Arrange
        var job1 = CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060));
        job1.UpdateEstimates(1000m, 1000m); // High revenue
        
        var job2 = CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851));
        job2.UpdateEstimates(100m, 100m); // Low revenue
        
        var job3 = CreateTestJob("JOB003", new Coordinate(34.0522, -118.2437));
        job3.UpdateEstimates(5000m, 5000m); // Very high revenue

        var jobs = new List<ServiceJob> { job1, job2, job3 };
        var parameters = CreateBasicParametersWithObjective(jobs, OptimizationObjective.MaximizeRevenue);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(3);
        // High revenue jobs should be prioritized despite distance
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldRespectWorkingHours()
    {
        // Arrange
        var jobs = new List<ServiceJob>
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(34.0522, -118.2437))
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        foreach (var stop in result.OptimizedStops)
        {
            var arrivalTime = stop.EstimatedArrival.TimeOfDay;
            // Arrival should be within reasonable working hours
            arrivalTime.Should().BeGreaterThanOrEqualTo(TimeSpan.FromHours(6));
            arrivalTime.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(20));
        }
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldHaveSequentialOrdering()
    {
        // Arrange
        var jobs = new List<ServiceJob>
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(34.0522, -118.2437)),
            CreateTestJob("JOB003", new Coordinate(41.8781, -87.6298))
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.OptimizedStops.Should().NotBeEmpty();
        for (int i = 0; i < result.OptimizedStops.Count; i++)
        {
            result.OptimizedStops[i].SequenceOrder.Should().Be(i + 1);
        }
    }

    // Helper methods
    private RouteOptimizationParameters CreateBasicParameters(IEnumerable<ServiceJob> jobs)
    {
        var technician = CreateBasicTechnician();
        return new RouteOptimizationParameters
        {
            Jobs = jobs.ToList(),
            Technician = technician,
            StartLocation = new Coordinate(40.7128, -74.0060),
            EndLocation = new Coordinate(40.7128, -74.0060),
            StartTime = DateTime.Today.AddHours(8),
            EndTime = DateTime.Today.AddHours(17),
            Objective = OptimizationObjective.MinimizeDistance,
            ValidateSkills = false,
            RespectTimeWindows = false,
            Constraints = new List<RouteConstraint>()
        };
    }

    private RouteOptimizationParameters CreateBasicParametersWithObjective(
        IEnumerable<ServiceJob> jobs, 
        OptimizationObjective objective)
    {
        var technician = CreateBasicTechnician();
        return new RouteOptimizationParameters
        {
            Jobs = jobs.ToList(),
            Technician = technician,
            StartLocation = new Coordinate(40.7128, -74.0060),
            EndLocation = new Coordinate(40.7128, -74.0060),
            StartTime = DateTime.Today.AddHours(8),
            EndTime = DateTime.Today.AddHours(17),
            Objective = objective,
            ValidateSkills = false,
            RespectTimeWindows = false,
            Constraints = new List<RouteConstraint>()
        };
    }

    private RouteOptimizationParameters CreateParametersWithSkillValidation(
        IEnumerable<ServiceJob> jobs, 
        Technician technician)
    {
        return new RouteOptimizationParameters
        {
            Jobs = jobs.ToList(),
            Technician = technician,
            StartLocation = new Coordinate(40.7128, -74.0060),
            EndLocation = new Coordinate(40.7128, -74.0060),
            StartTime = DateTime.Today.AddHours(8),
            EndTime = DateTime.Today.AddHours(17),
            Objective = OptimizationObjective.MinimizeDistance,
            ValidateSkills = true,
            RespectTimeWindows = false,
            Constraints = new List<RouteConstraint>()
        };
    }

    private Technician CreateBasicTechnician()
    {
        return new Technician(
            "TECH001",
            "John",
            "Doe",
            "john.doe@example.com",
            "TENANT001");
    }

    private Technician CreateTechnicianWithSkills(string[] skills)
    {
        var technician = CreateBasicTechnician();
        foreach (var skill in skills)
        {
            technician.AddSkill(skill);
        }
        return technician;
    }

    private ServiceJob CreateTestJob(string jobNumber, Coordinate location)
    {
        var address = new Address(
            "123 Test St",
            null,
            "New York",
            "NY",
            "10001",
            "USA",
            location);

        var job = new ServiceJob(
            jobNumber,
            $"Test Customer {jobNumber}",
            address,
            "Test Description",
            DateTime.Today.AddDays(1),
            TimeSpan.FromHours(1),
            "TENANT001",
            JobPriority.Medium);
        
        job.UpdateJobType(JobType.Installation);
        job.UpdateEstimates(1000m, 500m);
        
        return job;
    }

    private ServiceJob CreateTestJobWithSkills(string jobNumber, Coordinate location, string[] skills)
    {
        var job = CreateTestJob(jobNumber, location);
        foreach (var skill in skills)
        {
            job.AddRequiredSkill(skill);
        }
        return job;
    }
}
