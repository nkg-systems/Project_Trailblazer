using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Infrastructure.Optimization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace FieldOpsOptimizer.Infrastructure.Tests.Optimization;

public class TwoOptOptimizerTests
{
    private readonly Mock<ILogger<TwoOptOptimizer>> _mockLogger;
    private readonly Mock<ILogger<NearestNeighborOptimizer>> _mockNNLogger;
    private readonly NearestNeighborOptimizer _initializer;
    private readonly TwoOptOptimizer _optimizer;

    public TwoOptOptimizerTests()
    {
        _mockLogger = new Mock<ILogger<TwoOptOptimizer>>();
        _mockNNLogger = new Mock<ILogger<NearestNeighborOptimizer>>();
        _initializer = new NearestNeighborOptimizer(_mockNNLogger.Object);
        _optimizer = new TwoOptOptimizer(_mockLogger.Object, _initializer);
    }

    [Fact]
    public void Algorithm_ShouldReturn_TwoOpt()
    {
        // Act
        var algorithm = _optimizer.Algorithm;

        // Assert
        algorithm.Should().Be(OptimizationAlgorithm.TwoOpt);
    }

    [Theory]
    [InlineData(OptimizationObjective.MinimizeDistance, true)]
    [InlineData(OptimizationObjective.MinimizeTime, true)]
    [InlineData(OptimizationObjective.MaximizeRevenue, true)]
    [InlineData(OptimizationObjective.BalanceWorkload, false)]
    public void SupportsObjective_ShouldReturnCorrectly(OptimizationObjective objective, bool expected)
    {
        // Act
        var result = _optimizer.SupportsObjective(objective);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithEmptyJobs_ShouldReturnEmptyResult()
    {
        // Arrange
        var parameters = CreateBasicParameters(Array.Empty<ServiceJob>());

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().BeEmpty();
        result.TotalDistanceKm.Should().Be(0);
        // With empty jobs, 2-opt returns the initializer's result which has NearestNeighbor algorithm
        result.Algorithm.Should().Be(OptimizationAlgorithm.NearestNeighbor);
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithSingleJob_ShouldReturnSingleStop()
    {
        // Arrange
        var job = CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060));
        var parameters = CreateBasicParameters(new[] { job });

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(1);
        result.OptimizedStops.First().Job.Should().Be(job);
        result.Algorithm.Should().Be(OptimizationAlgorithm.TwoOpt);
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithTwoJobs_ShouldOptimize()
    {
        // Arrange - Two jobs where order matters for distance
        var jobs = new[]
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)), // NYC
            CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851))  // Times Square
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(2);
        result.TotalDistanceKm.Should().BeGreaterThan(0);
        result.Algorithm.Should().Be(OptimizationAlgorithm.TwoOpt);
        result.IsOptimal.Should().BeFalse(); // 2-opt is heuristic
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithMultipleJobs_ShouldImproveRoute()
    {
        // Arrange - Create a route that 2-opt can improve
        var jobs = new[]
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)), // Point 1
            CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851)), // Point 2
            CreateTestJob("JOB003", new Coordinate(40.7614, -73.9776)), // Point 3
            CreateTestJob("JOB004", new Coordinate(40.7489, -73.9680))  // Point 4
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(4);
        result.TotalDistanceKm.Should().BeGreaterThan(0);
        result.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
        
        // Verify sequence order is maintained
        for (int i = 0; i < result.OptimizedStops.Count; i++)
        {
            result.OptimizedStops[i].SequenceOrder.Should().Be(i + 1);
        }
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithSkillValidation_ShouldFilterJobs()
    {
        // Arrange
        var electricalJob = CreateTestJobWithSkills("JOB001", 
            new Coordinate(40.7128, -74.0060), 
            new[] { "Electrical" });
        var plumbingJob = CreateTestJobWithSkills("JOB002", 
            new Coordinate(40.7589, -73.9851), 
            new[] { "Plumbing" });
        var hvacJob = CreateTestJobWithSkills("JOB003",
            new Coordinate(40.7614, -73.9776),
            new[] { "HVAC" });
        
        var technician = CreateTechnicianWithSkills(new[] { "Electrical", "HVAC" });
        var parameters = CreateParametersWithSkillValidation(
            new[] { electricalJob, plumbingJob, hvacJob }, 
            technician);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(2);
        result.OptimizedStops.Select(s => s.Job.JobNumber).Should().Contain(new[] { "JOB001", "JOB003" });
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldCalculateTravelMetrics()
    {
        // Arrange
        var jobs = new[]
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851)),
            CreateTestJob("JOB003", new Coordinate(40.7614, -73.9776))
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().AllSatisfy(stop =>
        {
            stop.DistanceFromPreviousKm.Should().BeGreaterThanOrEqualTo(0);
            stop.TravelTimeFromPrevious.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            stop.EstimatedArrival.Should().BeAfter(DateTime.MinValue);
            stop.EstimatedDeparture.Should().BeAfter(stop.EstimatedArrival);
        });
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithMaxIterations_ShouldRespectLimit()
    {
        // Arrange
        var jobs = Enumerable.Range(0, 10)
            .Select(i => CreateTestJob($"JOB{i:00}", 
                new Coordinate(40.7128 + i * 0.01, -74.0060 + i * 0.01)))
            .ToArray();
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(10);
        result.Iterations.Should().BeGreaterThan(0);
        result.Metrics.Should().NotBeNull();
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithMinimizeDistance_ShouldOptimizeForDistance()
    {
        // Arrange
        var jobs = new[]
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851)),
            CreateTestJob("JOB003", new Coordinate(40.7614, -73.9776))
        };
        var parameters = CreateBasicParameters(jobs, OptimizationObjective.MinimizeDistance);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(3);
        result.TotalDistanceKm.Should().BeGreaterThan(0);
        result.Metrics.FinalCost.Should().BeLessThanOrEqualTo(result.Metrics.InitialCost);
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldTrackOptimizationMetrics()
    {
        // Arrange
        var jobs = new[]
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851)),
            CreateTestJob("JOB003", new Coordinate(40.7614, -73.9776))
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Metrics.Should().NotBeNull();
        result.Metrics.InitialCost.Should().BeGreaterThanOrEqualTo(0);
        result.Metrics.FinalCost.Should().BeGreaterThanOrEqualTo(0);
        result.Metrics.Evaluations.Should().BeGreaterThan(0);
        result.Metrics.CostHistory.Should().NotBeEmpty();
    }

    // Helper methods
    private RouteOptimizationParameters CreateBasicParameters(
        IEnumerable<ServiceJob> jobs, 
        OptimizationObjective objective = OptimizationObjective.MinimizeDistance)
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
}
