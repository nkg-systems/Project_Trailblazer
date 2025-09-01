using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.ValueObjects;

namespace FieldOpsOptimizer.Infrastructure.ExternalServices;

public class OSRMRoutingService : IRoutingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OSRMRoutingService> _logger;
    private readonly OSRMOptions _options;
    private readonly ResiliencePipeline _resiliencePipeline;

    public OSRMRoutingService(
        HttpClient httpClient,
        ILogger<OSRMRoutingService> logger,
        IOptions<OSRMOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        
        // Simplified resilience pipeline for now
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    public async Task<RouteMatrix> GetDistanceMatrixAsync(
        IEnumerable<Coordinate> coordinates,
        CancellationToken cancellationToken = default)
    {
        var coordinateList = coordinates.ToList();
        
        if (coordinateList.Count < 2)
            throw new ArgumentException("At least 2 coordinates are required for distance matrix");

        var coordinatesParam = string.Join(";", coordinateList.Select(c => $"{c.Longitude},{c.Latitude}"));
        var url = $"{_options.BaseUrl}/table/v1/driving/{coordinatesParam}?annotations=distance,duration";

        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(async _ =>
            {
                _logger.LogInformation("Calling OSRM table API: {Url}", url);
                return await _httpClient.GetAsync(url, cancellationToken);
            }, cancellationToken);

            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OSRMTableResponse>(content, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (result?.Code != "Ok")
            {
                throw new InvalidOperationException($"OSRM API error: {result?.Code} - {result?.Message}");
            }

            var size = coordinateList.Count;
            var distances = new double[size, size];
            var durations = new double[size, size];

            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    distances[i, j] = result.Distances[i][j] / 1000.0; // Convert to kilometers
                    durations[i, j] = result.Durations[i][j]; // Already in seconds
                }
            }

            return new RouteMatrix(distances, durations, coordinateList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OSRM table API");
            throw;
        }
    }

    public async Task<OptimizedRoute> OptimizeRouteAsync(
        OptimizeRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        // For basic optimization, we'll use a simple nearest neighbor approach
        // In a production system, you'd implement more sophisticated algorithms
        
        var coordinates = new List<Coordinate> { request.StartLocation };
        coordinates.AddRange(request.Waypoints.Select(w => w.Location));
        
        if (request.EndLocation != null)
        {
            coordinates.Add(request.EndLocation);
        }

        // Get distance matrix
        var matrix = await GetDistanceMatrixAsync(coordinates, cancellationToken);
        
        // Simple nearest neighbor optimization starting from index 0 (start location)
        var unvisited = new HashSet<int>(Enumerable.Range(1, request.Waypoints.Count));
        var optimizedOrder = new List<int> { 0 }; // Start with starting location
        var totalDistance = 0.0;
        var totalDuration = TimeSpan.Zero;
        
        int currentIndex = 0;
        
        while (unvisited.Any())
        {
            var nearest = unvisited.MinBy(i => matrix.Distances[currentIndex, i]);
            optimizedOrder.Add(nearest);
            
            totalDistance += matrix.Distances[currentIndex, nearest];
            totalDuration += TimeSpan.FromSeconds(matrix.Durations[currentIndex, nearest]);
            
            unvisited.Remove(nearest);
            currentIndex = nearest;
        }

        // If round trip and no specific end location, return to start
        if (request.RoundTrip && request.EndLocation == null)
        {
            totalDistance += matrix.Distances[currentIndex, 0];
            totalDuration += TimeSpan.FromSeconds(matrix.Durations[currentIndex, 0]);
            optimizedOrder.Add(0);
        }
        else if (request.EndLocation != null)
        {
            var endIndex = coordinates.Count - 1;
            totalDistance += matrix.Distances[currentIndex, endIndex];
            totalDuration += TimeSpan.FromSeconds(matrix.Durations[currentIndex, endIndex]);
            optimizedOrder.Add(endIndex);
        }

        // Build optimized waypoints (skip start location index 0)
        var optimizedWaypoints = optimizedOrder
            .Skip(1)
            .Where(i => i > 0 && i <= request.Waypoints.Count)
            .Select(i => request.Waypoints[i - 1])
            .ToList();

        // Add service times
        foreach (var waypoint in optimizedWaypoints)
        {
            totalDuration += waypoint.ServiceTime;
        }

        // Create segments (simplified)
        var segments = new List<RouteSegment>();
        for (int i = 0; i < optimizedOrder.Count - 1; i++)
        {
            var fromIndex = optimizedOrder[i];
            var toIndex = optimizedOrder[i + 1];
            
            segments.Add(new RouteSegment(
                coordinates[fromIndex],
                coordinates[toIndex],
                matrix.Distances[fromIndex, toIndex],
                TimeSpan.FromSeconds(matrix.Durations[fromIndex, toIndex]),
                "Drive to next location"));
        }

        return new OptimizedRoute(optimizedWaypoints, totalDistance, totalDuration, segments);
    }

    public async Task<NavigationRoute> GetNavigationRouteAsync(
        Coordinate start,
        Coordinate end,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_options.BaseUrl}/route/v1/driving/{start.Longitude},{start.Latitude};{end.Longitude},{end.Latitude}?geometries=geojson&steps=true";

        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(async _ =>
            {
                _logger.LogInformation("Calling OSRM route API: {Url}", url);
                return await _httpClient.GetAsync(url, cancellationToken);
            }, cancellationToken);

            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OSRMRouteResponse>(content, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (result?.Code != "Ok" || result.Routes?.Length == 0)
            {
                throw new InvalidOperationException($"OSRM API error: {result?.Code} - {result?.Message}");
            }

            var route = result.Routes[0];
            
            // Extract path coordinates from geometry
            var path = route.Geometry.Coordinates
                .Select(coord => new Coordinate(coord[1], coord[0])) // GeoJSON is [lon, lat]
                .ToList();

            // Extract instructions from steps
            var instructions = new List<RouteInstruction>();
            foreach (var leg in route.Legs)
            {
                foreach (var step in leg.Steps)
                {
                    instructions.Add(new RouteInstruction(
                        step.Maneuver.Instruction,
                        step.Distance / 1000.0, // Convert to km
                        TimeSpan.FromSeconds(step.Duration),
                        new Coordinate(step.Maneuver.Location[1], step.Maneuver.Location[0])));
                }
            }

            return new NavigationRoute(
                path,
                route.Distance / 1000.0, // Convert to km
                TimeSpan.FromSeconds(route.Duration),
                instructions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OSRM route API");
            throw;
        }
    }
}

public class OSRMOptions
{
    public const string SectionName = "OSRM";
    public string BaseUrl { get; set; } = "http://localhost:5000";
}

// OSRM API Response Models
internal record OSRMTableResponse(
    string Code,
    string? Message,
    double[][] Distances,
    double[][] Durations);

internal record OSRMRouteResponse(
    string Code,
    string? Message,
    OSRMRoute[] Routes);

internal record OSRMRoute(
    double Distance,
    double Duration,
    OSRMGeometry Geometry,
    OSRMLeg[] Legs);

internal record OSRMGeometry(
    double[][] Coordinates);

internal record OSRMLeg(
    double Distance,
    double Duration,
    OSRMStep[] Steps);

internal record OSRMStep(
    double Distance,
    double Duration,
    OSRMManeuver Maneuver);

internal record OSRMManeuver(
    string Instruction,
    double[] Location);
