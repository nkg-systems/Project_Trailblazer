using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FieldOpsOptimizer.Infrastructure.Optimization;

/// <summary>
/// Simulated Annealing algorithm for route optimization
/// Uses probabilistic acceptance criteria to escape local optima
/// Excellent balance between solution quality and execution time
/// </summary>
public class SimulatedAnnealingOptimizer : IRouteOptimizer
{
    private readonly ILogger<SimulatedAnnealingOptimizer> _logger;
    private readonly NearestNeighborOptimizer _initializer;
    private readonly Random _random = new();

    public OptimizationAlgorithm Algorithm => OptimizationAlgorithm.SimulatedAnnealing;

    // Simulated Annealing parameters
    private const double InitialTemperature = 1000.0;
    private const double CoolingRate = 0.995;
    private const double MinimumTemperature = 0.1;
    private const int MaxIterationsAtTemperature = 100;

    public SimulatedAnnealingOptimizer(
        ILogger<SimulatedAnnealingOptimizer> logger,
        NearestNeighborOptimizer initializer)
    {
        _logger = logger;
        _initializer = initializer;
    }

    public bool SupportsObjective(OptimizationObjective objective)
    {
        return objective is OptimizationObjective.MinimizeDistance or 
               OptimizationObjective.MinimizeTime or 
               OptimizationObjective.MaximizeRevenue;
    }

    public async Task<RouteOptimizationResult> OptimizeRouteAsync(
        RouteOptimizationParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("Starting simulated annealing optimization for {JobCount} jobs", parameters.Jobs.Count);

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

            // Apply simulated annealing
            var (improvedStops, metrics) = ApplySimulatedAnnealing(
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
                metrics);

            _logger.LogInformation("Simulated annealing completed in {Duration}ms. " +
                "Distance: {Distance:F2}km (improvement: {Improvement:F1}%)",
                stopwatch.ElapsedMilliseconds, result.TotalDistanceKm, result.Metrics.ImprovementPercentage);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during simulated annealing optimization");
            throw;
        }
    }

    private (List<OptimizedRouteStop> stops, OptimizationMetrics metrics) ApplySimulatedAnnealing(
        List<OptimizedRouteStop> initialStops,
        RouteOptimizationParameters parameters,
        DistanceMatrix distanceMatrix,
        CancellationToken cancellationToken)
    {
        var currentRoute = initialStops.Select(s => s.Job).ToList();
        var bestRoute = new List<ServiceJob>(currentRoute);
        var currentCost = CalculateRouteCost(currentRoute, distanceMatrix, parameters.Objective);
        var bestCost = currentCost;
        var initialCost = currentCost;

        var temperature = InitialTemperature;
        var iterations = 0;
        var acceptedMoves = 0;
        var rejectedMoves = 0;
        var costHistory = new List<double> { currentCost };

        _logger.LogDebug("Initial solution cost: {Cost:F2}, temperature: {Temp:F2}", currentCost, temperature);

        while (temperature > MinimumTemperature && !cancellationToken.IsCancellationRequested)
        {
            for (int iter = 0; iter < MaxIterationsAtTemperature; iter++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                iterations++;

                // Generate neighbor solution
                var neighborRoute = GenerateNeighbor(currentRoute);
                var neighborCost = CalculateRouteCost(neighborRoute, distanceMatrix, parameters.Objective);

                // Calculate cost difference
                var costDelta = neighborCost - currentCost;

                // Accept or reject neighbor
                bool accept = false;
                if (costDelta < 0)
                {
                    // Always accept better solutions
                    accept = true;
                }
                else
                {
                    // Probabilistically accept worse solutions
                    var acceptanceProbability = Math.Exp(-costDelta / temperature);
                    accept = _random.NextDouble() < acceptanceProbability;
                }

                if (accept)
                {
                    currentRoute = neighborRoute;
                    currentCost = neighborCost;
                    acceptedMoves++;

                    // Update best solution if improved
                    if (currentCost < bestCost)
                    {
                        bestRoute = new List<ServiceJob>(currentRoute);
                        bestCost = currentCost;
                        costHistory.Add(bestCost);
                        
                        _logger.LogDebug("New best solution found: {Cost:F2} at temperature {Temp:F2}", 
                            bestCost, temperature);
                    }
                }
                else
                {
                    rejectedMoves++;
                }
            }

            // Cool down temperature
            temperature *= CoolingRate;
        }

        var metrics = new OptimizationMetrics
        {
            InitialCost = initialCost,
            FinalCost = bestCost,
            Evaluations = iterations,
            CostHistory = costHistory,
            AdditionalMetrics = new Dictionary<string, object>
            {
                ["InitialTemperature"] = InitialTemperature,
                ["FinalTemperature"] = temperature,
                ["CoolingRate"] = CoolingRate,
                ["AcceptedMoves"] = acceptedMoves,
                ["RejectedMoves"] = rejectedMoves,
                ["AcceptanceRate"] = iterations > 0 ? (double)acceptedMoves / iterations : 0
            }
        };

        _logger.LogDebug("SA completed: {Iterations} iterations, {Accepted} accepted, {Rejected} rejected",
            iterations, acceptedMoves, rejectedMoves);

        return (BuildOptimizedStops(bestRoute, parameters, distanceMatrix), metrics);
    }

    private List<ServiceJob> GenerateNeighbor(List<ServiceJob> route)
    {
        if (route.Count < 2) return new List<ServiceJob>(route);

        var neighborRoute = new List<ServiceJob>(route);
        var neighborType = _random.Next(3); // 0: swap, 1: reverse segment, 2: insert

        switch (neighborType)
        {
            case 0: // Swap two random jobs
                SwapNeighbor(neighborRoute);
                break;
            case 1: // Reverse a random segment
                ReverseSegmentNeighbor(neighborRoute);
                break;
            case 2: // Insert a job at a different position
                InsertNeighbor(neighborRoute);
                break;
        }

        return neighborRoute;
    }

    private void SwapNeighbor(List<ServiceJob> route)
    {
        int i = _random.Next(route.Count);
        int j = _random.Next(route.Count);
        (route[i], route[j]) = (route[j], route[i]);
    }

    private void ReverseSegmentNeighbor(List<ServiceJob> route)
    {
        if (route.Count < 2) return;

        int start = _random.Next(route.Count);
        int length = _random.Next(2, Math.Min(route.Count - start + 1, route.Count / 2 + 1));
        
        route.Reverse(start, Math.Min(length, route.Count - start));
    }

    private void InsertNeighbor(List<ServiceJob> route)
    {
        if (route.Count < 2) return;

        int fromIndex = _random.Next(route.Count);
        int toIndex = _random.Next(route.Count);
        
        if (fromIndex != toIndex)
        {
            var job = route[fromIndex];
            route.RemoveAt(fromIndex);
            route.Insert(toIndex > fromIndex ? toIndex - 1 : toIndex, job);
        }
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
                OptimizationObjective.MaximizeRevenue => -CalculateRevenueMetric(fromIndex, toIndex, route[i + 1], distanceMatrix),
                _ => distanceMatrix.GetDistance(fromIndex, toIndex)
            };
        }

        return totalCost;
    }

    private double CalculateRevenueMetric(
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
        var revenueBenefit = (double)job.EstimatedRevenue;
        
        return revenueBenefit - travelCost;
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

    private Task<DistanceMatrix> BuildDistanceMatrixAsync(
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

        return Task.FromResult(new DistanceMatrix
        {
            Locations = locations,
            Distances = distances,
            Durations = durations
        });
    }

    private RouteOptimizationResult BuildOptimizationResult(
        List<OptimizedRouteStop> optimizedStops,
        RouteOptimizationParameters parameters,
        DistanceMatrix distanceMatrix,
        TimeSpan optimizationTime,
        OptimizationMetrics metrics)
    {
        var totalDistance = optimizedStops.Sum(s => s.DistanceFromPreviousKm);
        var totalDuration = optimizedStops.Sum(s => s.TravelTimeFromPrevious.Ticks) + 
            optimizedStops.Sum(s => s.Job.EstimatedDuration.Ticks);
        
        var totalCost = CalculateTotalCost(optimizedStops, parameters.Technician.HourlyRate);
        var violations = optimizedStops.SelectMany(s => s.ConstraintViolations).ToList();

        return new RouteOptimizationResult
        {
            OptimizedStops = optimizedStops,
            TotalDistanceKm = totalDistance,
            TotalDuration = TimeSpan.FromTicks(totalDuration),
            TotalCost = totalCost,
            Algorithm = Algorithm,
            OptimizationTime = optimizationTime,
            Iterations = metrics.Evaluations,
            IsOptimal = false, // SA is a heuristic
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
