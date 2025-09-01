using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Infrastructure.Optimization;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FieldOpsOptimizer.Infrastructure.Tests.Optimization;

public class RouteOptimizationTests
{
    private readonly Mock<ILogger<NearestNeighborOptimizer>> _nearestNeighborLogger;
    private readonly Mock<ILogger<TwoOptOptimizer>> _twoOptLogger;
    private readonly Mock<ILogger<GeneticOptimizer>> _geneticLogger;

    public RouteOptimizationTests()
    {
        _nearestNeighborLogger = new Mock<ILogger<NearestNeighborOptimizer>>();
        _twoOptLogger = new Mock<ILogger<TwoOptOptimizer>>();
        _geneticLogger = new Mock<ILogger<GeneticOptimizer>>();
    }

    [Fact]
    public async Task NearestNeighborOptimizer_WithValidJobs_ReturnsOptimizedRoute()
    {
        // Arrange
        var optimizer = new NearestNeighborOptimizer(_nearestNeighborLogger.Object);
        var parameters = CreateTestOptimizationParameters();

        // Act
        var result = await optimizer.OptimizeRouteAsync(parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.OptimizedStops.Count);
        Assert.Equal(OptimizationAlgorithm.NearestNeighbor, result.Algorithm);
        Assert.True(result.TotalDistanceKm > 0);
        Assert.True(result.TotalDuration > TimeSpan.Zero);
    }

    [Fact]
    public async Task TwoOptOptimizer_WithValidJobs_ImprovesSolution()
    {
        // Arrange
        var nearestNeighbor = new NearestNeighborOptimizer(_nearestNeighborLogger.Object);
        var twoOptOptimizer = new TwoOptOptimizer(_twoOptLogger.Object, nearestNeighbor);
        var parameters = CreateTestOptimizationParameters();

        // Act
        var nearestNeighborResult = await nearestNeighbor.OptimizeRouteAsync(parameters);
        var twoOptResult = await twoOptOptimizer.OptimizeRouteAsync(parameters);

        // Assert
        Assert.NotNull(twoOptResult);
        Assert.Equal(3, twoOptResult.OptimizedStops.Count);
        Assert.Equal(OptimizationAlgorithm.TwoOpt, twoOptResult.Algorithm);
        
        // 2-opt should generally produce better or equal results
        Assert.True(twoOptResult.TotalDistanceKm <= nearestNeighborResult.TotalDistanceKm);
    }

    [Fact]
    public async Task GeneticOptimizer_WithValidJobs_ReturnsOptimizedRoute()
    {
        // Arrange
        var optimizer = new GeneticOptimizer(_geneticLogger.Object);
        var parameters = CreateTestOptimizationParameters();

        // Act
        var result = await optimizer.OptimizeRouteAsync(parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.OptimizedStops.Count);
        Assert.Equal(OptimizationAlgorithm.Genetic, result.Algorithm);
        Assert.True(result.TotalDistanceKm > 0);
        Assert.True(result.Metrics.Evaluations > 0);
    }

    [Fact]
    public async Task RouteOptimizer_WithEmptyJobs_ReturnsEmptyResult()
    {
        // Arrange
        var optimizer = new NearestNeighborOptimizer(_nearestNeighborLogger.Object);
        var parameters = new RouteOptimizationParameters
        {
            Jobs = new List<ServiceJob>(),
            Technician = CreateTestTechnician()
        };

        // Act
        var result = await optimizer.OptimizeRouteAsync(parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.OptimizedStops);
        Assert.Equal(0, result.TotalDistanceKm);
    }

    [Fact]
    public async Task RouteOptimizer_WithSkillValidation_FiltersInvalidJobs()
    {
        // Arrange
        var optimizer = new NearestNeighborOptimizer(_nearestNeighborLogger.Object);
        var technician = CreateTestTechnician();
        technician.Skills.Clear();
        technician.Skills.Add("Basic Repair"); // Only has one skill

        var jobs = CreateTestJobs();
        jobs[0].RequiredSkills.Add("Advanced Diagnostics"); // Requires skill technician doesn't have

        var parameters = new RouteOptimizationParameters
        {
            Jobs = jobs,
            Technician = technician,
            ValidateSkills = true
        };

        // Act
        var result = await optimizer.OptimizeRouteAsync(parameters);

        // Assert
        Assert.NotNull(result);
        // Should only include jobs the technician can perform
        Assert.True(result.OptimizedStops.Count < 3);
    }

    [Theory]
    [InlineData(OptimizationObjective.MinimizeDistance)]
    [InlineData(OptimizationObjective.MinimizeTime)]
    [InlineData(OptimizationObjective.MinimizeCost)]
    public async Task RouteOptimizer_WithDifferentObjectives_ReturnsValidResults(OptimizationObjective objective)
    {
        // Arrange
        var optimizer = new NearestNeighborOptimizer(_nearestNeighborLogger.Object);
        var parameters = CreateTestOptimizationParameters();
        parameters = parameters with { Objective = objective };

        // Act
        var result = await optimizer.OptimizeRouteAsync(parameters);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.OptimizedStops.Count);
        Assert.True(result.TotalDistanceKm > 0);
    }

    [Fact]
    public void DistanceMatrix_CalculateRouteDistance_ReturnsCorrectTotal()
    {
        // Arrange
        var locations = new List<Coordinate>
        {
            new(40.7128, -74.0060), // New York
            new(34.0522, -118.2437), // Los Angeles  
            new(41.8781, -87.6298)   // Chicago
        };

        var matrix = BuildTestDistanceMatrix(locations);

        // Act
        var distance = matrix.CalculateRouteDistance(new[] { 0, 1, 2 });

        // Assert
        Assert.True(distance > 0);
        Assert.Equal(matrix.GetDistance(0, 1) + matrix.GetDistance(1, 2), distance);
    }

    private RouteOptimizationParameters CreateTestOptimizationParameters()
    {
        return new RouteOptimizationParameters
        {
            Jobs = CreateTestJobs(),
            Technician = CreateTestTechnician(),
            Objective = OptimizationObjective.MinimizeDistance,
            StartLocation = new Coordinate(40.7128, -74.0060) // New York
        };
    }

    private List<ServiceJob> CreateTestJobs()
    {
        return new List<ServiceJob>
        {
            CreateTestJob("JOB001", "123 Main St", 40.7580, -73.9855, "Basic Repair"),
            CreateTestJob("JOB002", "456 Oak Ave", 40.7282, -73.7949, "Installation"),  
            CreateTestJob("JOB003", "789 Pine St", 40.6892, -74.0445, "Maintenance")
        };
    }

    private ServiceJob CreateTestJob(string jobNumber, string street, double lat, double lng, string skill)
    {
        var address = new Address(street, null, "New York", "NY", "10001", "US", new Coordinate(lat, lng));
        
        return new ServiceJob(
            jobNumber,
            "Test Customer",
            address,
            "Test service",
            JobStatus.Scheduled,
            JobPriority.Medium,
            DateTime.Today.AddHours(9),
            TimeSpan.FromHours(1),
            "test-tenant",
            new List<string> { skill });
    }

    private Technician CreateTestTechnician()
    {
        var homeAddress = new Address("100 Tech St", null, "New York", "NY", "10001", "US", new Coordinate(40.7128, -74.0060));
        
        var technician = new Technician(
            "TECH001",
            "John",
            "Doe", 
            "john.doe@test.com",
            homeAddress,
            50.0m,
            "test-tenant",
            new List<string> { "Basic Repair", "Installation", "Maintenance" },
            new List<WorkingHours> { new(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(17)) });

        return technician;
    }

    private DistanceMatrix BuildTestDistanceMatrix(List<Coordinate> locations)
    {
        var size = locations.Count;
        var distances = new double[size, size];
        var durations = new int[size, size];

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                if (i == j)
                {
                    distances[i, j] = 0;
                    durations[i, j] = 0;
                }
                else
                {
                    var distance = locations[i].DistanceToInKilometers(locations[j]);
                    distances[i, j] = distance;
                    durations[i, j] = (int)(distance / 40.0 * 3600);
                }
            }
        }

        return new DistanceMatrix
        {
            Locations = locations,
            Distances = distances,
            Durations = durations
        };
    }
}

/// <summary>
/// Performance benchmarks for optimization algorithms
/// </summary>
public class OptimizationBenchmarkTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task BenchmarkAlgorithms_WithVariousJobCounts_MeasuresPerformance(int jobCount)
    {
        // Arrange
        var jobs = GenerateRandomJobs(jobCount);
        var technician = CreateBenchmarkTechnician();
        var parameters = new RouteOptimizationParameters
        {
            Jobs = jobs,
            Technician = technician,
            Objective = OptimizationObjective.MinimizeDistance
        };

        var nearestNeighbor = new NearestNeighborOptimizer(Mock.Of<ILogger<NearestNeighborOptimizer>>());
        var twoOpt = new TwoOptOptimizer(Mock.Of<ILogger<TwoOptOptimizer>>(), nearestNeighbor);
        var genetic = new GeneticOptimizer(Mock.Of<ILogger<GeneticOptimizer>>());

        // Act & Assert
        var nnResult = await nearestNeighbor.OptimizeRouteAsync(parameters);
        var twoOptResult = await twoOpt.OptimizeRouteAsync(parameters);
        var geneticResult = await genetic.OptimizeRouteAsync(parameters);

        // Performance assertions
        Assert.True(nnResult.OptimizationTime < TimeSpan.FromSeconds(1)); // Should be very fast
        Assert.True(twoOptResult.OptimizationTime < TimeSpan.FromSeconds(5)); // Should be reasonably fast
        Assert.True(geneticResult.OptimizationTime < TimeSpan.FromSeconds(30)); // May take longer

        // Quality assertions (genetic should generally be best, then 2-opt, then nearest neighbor)
        Assert.True(geneticResult.TotalDistanceKm <= twoOptResult.TotalDistanceKm * 1.1); // Within 10%
        Assert.True(twoOptResult.TotalDistanceKm <= nnResult.TotalDistanceKm * 1.1); // Within 10%
    }

    private List<ServiceJob> GenerateRandomJobs(int count)
    {
        var random = new Random(42); // Fixed seed for reproducible tests
        var jobs = new List<ServiceJob>();

        for (int i = 0; i < count; i++)
        {
            // Generate random coordinates around New York area
            var lat = 40.7128 + (random.NextDouble() - 0.5) * 0.1; // ±0.05 degrees
            var lng = -74.0060 + (random.NextDouble() - 0.5) * 0.1;

            var address = new Address(
                $"{i + 100} Test St",
                null,
                "New York",
                "NY",
                "10001",
                "US",
                new Coordinate(lat, lng));

            var job = new ServiceJob(
                $"JOB{i:D3}",
                $"Customer {i}",
                address,
                $"Test service {i}",
                JobStatus.Scheduled,
                JobPriority.Medium,
                DateTime.Today.AddHours(9),
                TimeSpan.FromMinutes(30 + random.Next(60)), // 30-90 minutes
                "test-tenant",
                new List<string> { "Basic Repair" });

            jobs.Add(job);
        }

        return jobs;
    }

    private Technician CreateBenchmarkTechnician()
    {
        var homeAddress = new Address("100 Tech St", null, "New York", "NY", "10001", "US", new Coordinate(40.7128, -74.0060));
        
        return new Technician(
            "TECH001",
            "Test",
            "Technician",
            "test@test.com",
            homeAddress,
            50.0m,
            "test-tenant",
            new List<string> { "Basic Repair", "Installation", "Maintenance" },
            new List<WorkingHours> { new(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(17)) });
    }
}
