using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FieldOpsOptimizer.Infrastructure.Optimization;

/// <summary>
/// 2-opt local search algorithm for route optimization
/// Improves routes by eliminating crossing edges
/// </summary>
public class TwoOptOptimizer : IRouteOptimizer
{
    private readonly ILogger<TwoOptOptimizer> _logger;
    private readonly NearestNeighborOptimizer _initializer;

    public OptimizationAlgorithm Algorithm => OptimizationAlgorithm.TwoOpt;

    public TwoOptOptimizer(ILogger<TwoOptOptimizer> logger, NearestNeighborOptimizer initializer)
    {
        _logger = logger;
        _initializer = initializer;
    }

    public bool SupportsObjective(OptimizationObjective objective)
    {
        return objective is OptimizationObjective.MinimizeDistance or 
               OptimizationObjective.MinimizeTime or 
               OptimizationObjective.MinimizeCost;
    }

    public async Task<RouteOptimizationResult> OptimizeRouteAsync(
        RouteOptimizationParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("Starting 2-opt optimization for {JobCount} jobs", parameters.Jobs.Count);

        try
        {
            // Get initial solution using nearest neighbor
            var initialResult = await _initializer.OptimizeRouteAsync(parameters, cancellationToken);
            if (!initialResult.OptimizedStops.Any())
            {
                return initialResult;
            }

            var distanceMatrix = parameters.DistanceMatrix ?? 
                await BuildDistanceMatrixAsync(parameters.Jobs.ToList(), parameters.StartLocation, cancellationToken);

            // Apply 2-opt improvements
            var improvedStops = Apply2OptImprovement(
                initialResult.OptimizedStops.ToList(),
                parameters,
                distanceMatrix,
                cancellationToken);

            // Build final result
            var result = BuildOptimizationResult(
                improvedStops,
                parameters,
                distanceMatrix,
                stopwatch.Elapsed,
                initialResult.Metrics.InitialCost);

            _logger.LogInformation("2-opt optimization completed in {Duration}ms. " +
                "Distance: {Distance:F2}km (improvement: {Improvement:F1}%)",
                stopwatch.ElapsedMilliseconds, result.TotalDistanceKm, result.Metrics.ImprovementPercentage);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during 2-opt optimization");
            throw;
        }
    }

    private List<OptimizedRouteStop> Apply2OptImprovement(
        List<OptimizedRouteStop> initialStops,
        RouteOptimizationParameters parameters,
        DistanceMatrix distanceMatrix,
        CancellationToken cancellationToken)
    {
        var currentRoute = initialStops.Select(s => s.Job).ToList();
        var bestRoute = new List<ServiceJob>(currentRoute);
        var bestCost = CalculateRouteCost(bestRoute, distanceMatrix, parameters.Objective);
        
        bool improved = true;
        int iterations = 0;
        var maxIterations = Math.Min(1000, currentRoute.Count * currentRoute.Count);
        var costHistory = new List<double> { bestCost };

        while (improved && iterations < maxIterations && !cancellationToken.IsCancellationRequested)
        {
            improved = false;
            iterations++;

            // Try all possible 2-opt swaps
            for (int i = 0; i < currentRoute.Count - 1; i++)
            {
                for (int j = i + 1; j < currentRoute.Count; j++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Create new route by reversing the segment between i and j
                    var newRoute = Apply2OptSwap(currentRoute, i, j);
                    var newCost = CalculateRouteCost(newRoute, distanceMatrix, parameters.Objective);

                    if (newCost < bestCost)
                    {
                        bestRoute = newRoute;
                        bestCost = newCost;
                        currentRoute = new List<ServiceJob>(newRoute);
                        improved = true;
                        costHistory.Add(bestCost);
                        
                        _logger.LogDebug("2-opt improvement found: iteration {Iteration}, cost {Cost:F2}", 
                            iterations, bestCost);
                    }
                }
                if (cancellationToken.IsCancellationRequested) break;
            }
        }

        _logger.LogDebug("2-opt completed after {Iterations} iterations", iterations);

        // Convert back to OptimizedRouteStop with proper timing
        return BuildOptimizedStops(bestRoute, parameters, distanceMatrix);
    }

    private static List<ServiceJob> Apply2OptSwap(List<ServiceJob> route, int i, int j)
    {
        var newRoute = new List<ServiceJob>();
        
        // Add jobs before i unchanged
        newRoute.AddRange(route.Take(i));
        
        // Reverse the segment from i to j
        var segment = route.Skip(i).Take(j - i + 1).Reverse();
        newRoute.AddRange(segment);
        
        // Add jobs after j unchanged
        newRoute.AddRange(route.Skip(j + 1));
        
        return newRoute;
    }

    private double CalculateRouteCost(
        List<ServiceJob> route,
        DistanceMatrix distanceMatrix,
        OptimizationObjective objective)
    {
        if (!route.Any()) return 0;

        double totalCost = 0;
        
        for (int i = 0; i < route.Count - 1; i++)
        {
            var fromIndex = GetJobLocationIndex(route[i], distanceMatrix);
            var toIndex = GetJobLocationIndex(route[i + 1], distanceMatrix);
            
            totalCost += objective switch
            {
                OptimizationObjective.MinimizeDistance => distanceMatrix.GetDistance(fromIndex, toIndex),
                OptimizationObjective.MinimizeTime => distanceMatrix.GetDuration(fromIndex, toIndex).TotalMinutes,
                OptimizationObjective.MinimizeCost => CalculateCostMetric(fromIndex, toIndex, route[i + 1], distanceMatrix),
                _ => distanceMatrix.GetDistance(fromIndex, toIndex)
            };
        }

        return totalCost;
    }

    private double CalculateCostMetric(
        int fromLocationIndex,
        int toLocationIndex,
        ServiceJob job,
        DistanceMatrix distanceMatrix)
    {
        var distance = distanceMatrix.GetDistance(fromLocationIndex, toLocationIndex);
        var time = distanceMatrix.GetDuration(fromLocationIndex, toLocationIndex).TotalHours;
        
        const double costPerKm = 0.50;
        const double costPerHour = 25.0;
        
        var travelCost = (distance * costPerKm) + (time * costPerHour);
        var revenueBenefit = (double)job.EstimatedRevenue * 0.1;
        
        return travelCost - revenueBenefit;
    }

    private List<OptimizedRouteStop> BuildOptimizedStops(
        List<ServiceJob> optimizedRoute,
        RouteOptimizationParameters parameters,
        DistanceMatrix distanceMatrix)
    {
        var result = new List<OptimizedRouteStop>();
        var currentTime = parameters.Technician.WorkingHours.FirstOrDefault()?.StartTime ?? TimeSpan.FromHours(8);
        var currentDate = DateTime.Today.Add(currentTime);
        
        var currentLocation = parameters.StartLocation ?? GetJobLocation(optimizedRoute.First());
        var currentLocationIndex = GetLocationIndex(currentLocation, distanceMatrix);

        for (int i = 0; i < optimizedRoute.Count; i++)
        {
            var job = optimizedRoute[i];
            var jobLocationIndex = GetJobLocationIndex(job, distanceMatrix);
            
            var travelDistance = distanceMatrix.GetDistance(currentLocationIndex, jobLocationIndex);
            var travelTime = distanceMatrix.GetDuration(currentLocationIndex, jobLocationIndex);
            
            var estimatedArrival = currentDate.Add(travelTime);
            var estimatedDeparture = estimatedArrival.Add(job.EstimatedDuration);
            
            var violations = CheckConstraints(job, estimatedArrival, parameters);
            
            result.Add(new OptimizedRouteStop
            {
                Job = job,
                SequenceOrder = i + 1,
                DistanceFromPreviousKm = travelDistance,
                TravelTimeFromPrevious = travelTime,
                EstimatedArrival = estimatedArrival,
                EstimatedDeparture = estimatedDeparture,
                HasConstraintViolations = violations.Any(),
                ConstraintViolations = violations
            });

            currentLocationIndex = jobLocationIndex;
            currentDate = estimatedDeparture;
        }

        return result;
    }

    private static Coordinate GetJobLocation(ServiceJob job)
    {
        return job.ServiceAddress.Coordinate ?? Coordinate.Zero;
    }

    private static int GetLocationIndex(Coordinate location, DistanceMatrix matrix)
    {
        for (int i = 0; i < matrix.Locations.Count; i++)
        {
            if (Math.Abs(matrix.Locations[i].Latitude - location.Latitude) < 0.0001 &&
                Math.Abs(matrix.Locations[i].Longitude - location.Longitude) < 0.0001)
            {
                return i;
            }
        }
        return 0;
    }

    private static int GetJobLocationIndex(ServiceJob job, DistanceMatrix matrix)
    {
        var jobLocation = GetJobLocation(job);
        return GetLocationIndex(jobLocation, matrix);
    }

    private List<string> CheckConstraints(
        ServiceJob job,
        DateTime estimatedArrival,
        RouteOptimizationParameters parameters)
    {
        var violations = new List<string>();

        if (parameters.RespectTimeWindows && job.PreferredTimeWindow.HasValue)
        {
            var preferredStart = job.ScheduledDate.Date.Add(job.PreferredTimeWindow.Value);
            var preferredEnd = preferredStart.AddHours(2);

            if (estimatedArrival < preferredStart || estimatedArrival > preferredEnd)
            {
                violations.Add($"Arrival time {estimatedArrival:HH:mm} outside preferred window {preferredStart:HH:mm}-{preferredEnd:HH:mm}");
            }
        }

        var arrivalTime = estimatedArrival.TimeOfDay;
        var workingHours = parameters.Technician.WorkingHours.FirstOrDefault();
        if (workingHours != null)
        {
            if (arrivalTime < workingHours.StartTime || arrivalTime > workingHours.EndTime)
            {
                violations.Add($"Arrival time {arrivalTime} outside working hours {workingHours.StartTime}-{workingHours.EndTime}");
            }
        }

        return violations;
    }

    private async Task<DistanceMatrix> BuildDistanceMatrixAsync(
        List<ServiceJob> jobs,
        Coordinate? startLocation,
        CancellationToken cancellationToken)
    {
        var locations = new List<Coordinate>();
        
        if (startLocation != null)
        {
            locations.Add(startLocation);
        }

        locations.AddRange(jobs.Select(GetJobLocation));

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

    private RouteOptimizationResult BuildOptimizationResult(
        List<OptimizedRouteStop> optimizedStops,
        RouteOptimizationParameters parameters,
        DistanceMatrix distanceMatrix,
        TimeSpan optimizationTime,
        double initialCost)
    {
        var totalDistance = optimizedStops.Sum(s => s.DistanceFromPreviousKm);
        var totalDuration = optimizedStops.Sum(s => s.TravelTimeFromPrevious.Ticks) + 
            optimizedStops.Sum(s => s.Job.EstimatedDuration.Ticks);
        
        var totalCost = CalculateTotalCost(optimizedStops, parameters.Technician.HourlyRate);
        var violations = optimizedStops.SelectMany(s => s.ConstraintViolations).ToList();

        var metrics = new OptimizationMetrics
        {
            InitialCost = initialCost,
            FinalCost = totalDistance,
            Evaluations = optimizedStops.Count * optimizedStops.Count, // Rough estimate
            CostHistory = new[] { initialCost, totalDistance }
        };

        return new RouteOptimizationResult
        {
            OptimizedStops = optimizedStops,
            TotalDistanceKm = totalDistance,
            TotalDuration = TimeSpan.FromTicks(totalDuration),
            TotalCost = totalCost,
            Algorithm = Algorithm,
            OptimizationTime = optimizationTime,
            Iterations = 1,
            IsOptimal = false, // 2-opt is a heuristic
            ConstraintViolations = violations,
            Metrics = metrics
        };
    }

    private static decimal CalculateTotalCost(List<OptimizedRouteStop> stops, decimal hourlyRate)
    {
        var totalTimeHours = stops.Sum(s => s.TravelTimeFromPrevious.TotalHours + s.Job.EstimatedDuration.TotalHours);
        return (decimal)totalTimeHours * hourlyRate;
    }
}
