using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FieldOpsOptimizer.Infrastructure.Optimization;

/// <summary>
/// Service for calculating distance and time matrices for route optimization
/// </summary>
public interface IDistanceMatrixService
{
    /// <summary>
    /// Builds a distance matrix for the given locations using routing service
    /// </summary>
    Task<DistanceMatrix> BuildDistanceMatrixAsync(
        IReadOnlyList<Coordinate> locations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets distance and duration between two points
    /// </summary>
    Task<(double distanceKm, TimeSpan duration)> GetDistanceAndDurationAsync(
        Coordinate from,
        Coordinate to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds distance matrix using cached haversine distances (fallback)
    /// </summary>
    DistanceMatrix BuildHaversineDistanceMatrix(IReadOnlyList<Coordinate> locations);
}

public class DistanceMatrixService : IDistanceMatrixService
{
    private readonly IRoutingService _routingService;
    private readonly ILogger<DistanceMatrixService> _logger;

    // Cache for distance calculations
    private readonly Dictionary<(Coordinate from, Coordinate to), (double distance, TimeSpan duration)> _distanceCache = new();

    public DistanceMatrixService(IRoutingService routingService, ILogger<DistanceMatrixService> logger)
    {
        _routingService = routingService;
        _logger = logger;
    }

    public async Task<DistanceMatrix> BuildDistanceMatrixAsync(
        IReadOnlyList<Coordinate> locations,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building distance matrix for {LocationCount} locations", locations.Count);

        try
        {
            var size = locations.Count;
            var distances = new double[size, size];
            var durations = new int[size, size];

            // Calculate distances and durations for all pairs
            var tasks = new List<Task>();

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
                        // Capture variables for closure
                        int fromIndex = i, toIndex = j;
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var (distance, duration) = await GetDistanceAndDurationAsync(
                                    locations[fromIndex], 
                                    locations[toIndex], 
                                    cancellationToken);

                                distances[fromIndex, toIndex] = distance;
                                durations[fromIndex, toIndex] = (int)duration.TotalSeconds;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to get routing data for {From} to {To}, using haversine fallback",
                                    locations[fromIndex], locations[toIndex]);

                                // Fallback to haversine distance
                                var fallbackDistance = locations[fromIndex].DistanceToInKilometers(locations[toIndex]);
                                distances[fromIndex, toIndex] = fallbackDistance;
                                durations[fromIndex, toIndex] = (int)(fallbackDistance / 40.0 * 3600); // 40 km/h average
                            }
                        }, cancellationToken));
                    }
                }
            }

            // Wait for all distance calculations to complete
            await Task.WhenAll(tasks);

            _logger.LogInformation("Distance matrix built successfully");

            return new DistanceMatrix
            {
                Locations = locations,
                Distances = distances,
                Durations = durations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building distance matrix, falling back to haversine distances");
            return BuildHaversineDistanceMatrix(locations);
        }
    }

    public async Task<(double distanceKm, TimeSpan duration)> GetDistanceAndDurationAsync(
        Coordinate from,
        Coordinate to,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = (from, to);
        if (_distanceCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            // Use routing service distance matrix for two points
            var matrix = await _routingService.GetDistanceMatrixAsync(
                new[] { from, to },
                cancellationToken);

            if (matrix != null && matrix.Coordinates.Count >= 2)
            {
                var distance = matrix.Distances[0, 1];
                var duration = TimeSpan.FromSeconds(matrix.Durations[0, 1]);

                // Cache the result
                _distanceCache[cacheKey] = (distance, duration);
                _distanceCache[(to, from)] = (distance, duration); // Assume symmetric

                return (distance, duration);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Routing service failed for {From} to {To}, using haversine fallback", from, to);
        }

        // Fallback to haversine distance calculation
        var haversineDistance = from.DistanceToInKilometers(to);
        var estimatedDuration = TimeSpan.FromHours(haversineDistance / 40.0); // 40 km/h average speed

        var fallbackResult = (haversineDistance, estimatedDuration);
        _distanceCache[cacheKey] = fallbackResult;
        _distanceCache[(to, from)] = fallbackResult;

        return fallbackResult;
    }

    public DistanceMatrix BuildHaversineDistanceMatrix(IReadOnlyList<Coordinate> locations)
    {
        _logger.LogInformation("Building haversine distance matrix for {LocationCount} locations", locations.Count);

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
                    
                    // Estimate duration: 40 km/h average speed
                    var estimatedHours = distance / 40.0;
                    durations[i, j] = (int)(estimatedHours * 3600);
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
/// Extension methods for distance matrix operations
/// </summary>
public static class DistanceMatrixExtensions
{
    /// <summary>
    /// Finds the index of a coordinate in the distance matrix
    /// </summary>
    public static int FindLocationIndex(this DistanceMatrix matrix, Coordinate location)
    {
        for (int i = 0; i < matrix.Locations.Count; i++)
        {
            if (Math.Abs(matrix.Locations[i].Latitude - location.Latitude) < 0.0001 &&
                Math.Abs(matrix.Locations[i].Longitude - location.Longitude) < 0.0001)
            {
                return i;
            }
        }
        return -1; // Not found
    }

    /// <summary>
    /// Gets the total distance for a route sequence
    /// </summary>
    public static double CalculateRouteDistance(this DistanceMatrix matrix, IEnumerable<int> sequence)
    {
        var sequenceList = sequence.ToList();
        if (sequenceList.Count < 2) return 0.0;

        double totalDistance = 0.0;
        for (int i = 0; i < sequenceList.Count - 1; i++)
        {
            totalDistance += matrix.GetDistance(sequenceList[i], sequenceList[i + 1]);
        }

        return totalDistance;
    }

    /// <summary>
    /// Gets the total duration for a route sequence
    /// </summary>
    public static TimeSpan CalculateRouteDuration(this DistanceMatrix matrix, IEnumerable<int> sequence)
    {
        var sequenceList = sequence.ToList();
        if (sequenceList.Count < 2) return TimeSpan.Zero;

        var totalSeconds = 0;
        for (int i = 0; i < sequenceList.Count - 1; i++)
        {
            totalSeconds += matrix.Durations[sequenceList[i], sequenceList[i + 1]];
        }

        return TimeSpan.FromSeconds(totalSeconds);
    }

    /// <summary>
    /// Creates a subset distance matrix with only the specified location indices
    /// </summary>
    public static DistanceMatrix CreateSubset(this DistanceMatrix matrix, IEnumerable<int> indices)
    {
        var indexList = indices.ToList();
        var size = indexList.Count;
        var newDistances = new double[size, size];
        var newDurations = new int[size, size];
        var newLocations = indexList.Select(i => matrix.Locations[i]).ToList();

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                newDistances[i, j] = matrix.GetDistance(indexList[i], indexList[j]);
                newDurations[i, j] = matrix.Durations[indexList[i], indexList[j]];
            }
        }

        return new DistanceMatrix
        {
            Locations = newLocations,
            Distances = newDistances,
            Durations = newDurations
        };
    }
}
