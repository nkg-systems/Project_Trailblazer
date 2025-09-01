using FieldOpsOptimizer.Domain.ValueObjects;

namespace FieldOpsOptimizer.Application.Common.Interfaces;

public interface IRoutingService
{
    Task<RouteMatrix> GetDistanceMatrixAsync(
        IEnumerable<Coordinate> coordinates,
        CancellationToken cancellationToken = default);

    Task<OptimizedRoute> OptimizeRouteAsync(
        OptimizeRouteRequest request,
        CancellationToken cancellationToken = default);

    Task<NavigationRoute> GetNavigationRouteAsync(
        Coordinate start,
        Coordinate end,
        CancellationToken cancellationToken = default);
}

public record RouteMatrix(
    double[,] Distances,
    double[,] Durations,
    List<Coordinate> Coordinates);

public record OptimizeRouteRequest(
    Coordinate StartLocation,
    List<RouteWaypoint> Waypoints,
    Coordinate? EndLocation = null,
    bool RoundTrip = true);

public record RouteWaypoint(
    Coordinate Location,
    string Name,
    TimeSpan ServiceTime = default,
    TimeSpan? TimeWindow = null);

public record OptimizedRoute(
    List<RouteWaypoint> OptimizedWaypoints,
    double TotalDistance,
    TimeSpan TotalDuration,
    List<RouteSegment> Segments);

public record RouteSegment(
    Coordinate Start,
    Coordinate End,
    double Distance,
    TimeSpan Duration,
    string Instructions);

public record NavigationRoute(
    List<Coordinate> Path,
    double Distance,
    TimeSpan Duration,
    List<RouteInstruction> Instructions);

public record RouteInstruction(
    string Text,
    double Distance,
    TimeSpan Duration,
    Coordinate Location);
