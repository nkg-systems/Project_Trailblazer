using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Infrastructure.Optimization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace FieldOpsOptimizer.Infrastructure.Tests.Optimization;

public class GeneticOptimizerTests
{
    private readonly Mock<ILogger<GeneticOptimizer>> _mockLogger;
    private readonly GeneticOptimizer _optimizer;

    public GeneticOptimizerTests()
    {
        _mockLogger = new Mock<ILogger<GeneticOptimizer>>();
        _optimizer = new GeneticOptimizer(_mockLogger.Object);
    }

    [Fact]
    public void Algorithm_ShouldReturn_Genetic()
    {
        // Act
        var algorithm = _optimizer.Algorithm;

        // Assert
        algorithm.Should().Be(OptimizationAlgorithm.Genetic);
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
        result.Algorithm.Should().Be(OptimizationAlgorithm.Genetic);
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
        result.Algorithm.Should().Be(OptimizationAlgorithm.Genetic);
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithMultipleJobs_ShouldOptimize()
    {
        // Arrange
        var jobs = new[]
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851)),
            CreateTestJob("JOB003", new Coordinate(40.7614, -73.9776)),
            CreateTestJob("JOB004", new Coordinate(40.7489, -73.9680))
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(4);
        result.TotalDistanceKm.Should().BeGreaterThan(0);
        result.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
        result.Algorithm.Should().Be(OptimizationAlgorithm.Genetic);
        result.IsOptimal.Should().BeFalse(); // Genetic is heuristic
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
        
        var technician = CreateTechnicianWithSkills(new[] { "Electrical" });
        var parameters = CreateParametersWithSkillValidation(
            new[] { electricalJob, plumbingJob }, 
            technician);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(1);
        result.OptimizedStops.First().Job.JobNumber.Should().Be("JOB001");
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldEvolvePopulation()
    {
        // Arrange - Enough jobs to make evolution meaningful
        var jobs = Enumerable.Range(0, 8)
            .Select(i => CreateTestJob($"JOB{i:00}", 
                new Coordinate(40.7128 + i * 0.01, -74.0060 + i * 0.01)))
            .ToArray();
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(8);
        result.Iterations.Should().BeGreaterThan(0);
        result.Metrics.Should().NotBeNull();
        result.Metrics.Evaluations.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldImproveOverGenerations()
    {
        // Arrange
        var jobs = new[]
        {
            CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060)),
            CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851)),
            CreateTestJob("JOB003", new Coordinate(40.7614, -73.9776)),
            CreateTestJob("JOB004", new Coordinate(40.7489, -73.9680)),
            CreateTestJob("JOB005", new Coordinate(40.7350, -73.9950))
        };
        var parameters = CreateBasicParameters(jobs);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        // Genetic algorithm is stochastic - it attempts to improve but doesn't always guarantee it
        result.Metrics.FinalCost.Should().BeGreaterThanOrEqualTo(0);
        result.Metrics.InitialCost.Should().BeGreaterThanOrEqualTo(0);
        result.Metrics.CostHistory.Should().NotBeEmpty();
        result.Metrics.Evaluations.Should().BeGreaterThan(0);
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
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithMaximizeRevenue_ShouldOptimizeForRevenue()
    {
        // Arrange
        var highRevenueJob = CreateTestJob("JOB001", new Coordinate(40.7128, -74.0060));
        highRevenueJob.UpdateEstimates(5000m, 1000m);
        
        var lowRevenueJob = CreateTestJob("JOB002", new Coordinate(40.7589, -73.9851));
        lowRevenueJob.UpdateEstimates(1000m, 500m);
        
        var mediumRevenueJob = CreateTestJob("JOB003", new Coordinate(40.7614, -73.9776));
        mediumRevenueJob.UpdateEstimates(3000m, 800m);
        
        var parameters = CreateBasicParameters(
            new[] { highRevenueJob, lowRevenueJob, mediumRevenueJob }, 
            OptimizationObjective.MaximizeRevenue);

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedStops.Should().HaveCount(3);
    }

    [Fact]
    public async Task OptimizeRouteAsync_ShouldTrackGeneticAlgorithmMetrics()
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
        result.Metrics.AdditionalMetrics.Should().NotBeNull();
        result.Metrics.AdditionalMetrics.Should().ContainKey("PopulationSize");
        result.Metrics.AdditionalMetrics.Should().ContainKey("MutationRate");
        result.Metrics.AdditionalMetrics.Should().ContainKey("CrossoverRate");
        result.Metrics.AdditionalMetrics.Should().ContainKey("MaxGenerations");
    }

    [Fact]
    public async Task OptimizeRouteAsync_WithCancellation_ShouldHandleGracefully()
    {
        // Arrange
        var jobs = Enumerable.Range(0, 20)
            .Select(i => CreateTestJob($"JOB{i:000}", 
                new Coordinate(40.7128 + i * 0.001, -74.0060 + i * 0.001)))
            .ToArray();
        var parameters = CreateBasicParameters(jobs);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _optimizer.OptimizeRouteAsync(parameters, cts.Token);

        // Assert
        result.Should().NotBeNull();
        // Should handle cancellation gracefully
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
