using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FieldOpsOptimizer.Infrastructure.Optimization;

/// <summary>
/// Nearest neighbor greedy algorithm for route optimization
/// Fast but produces suboptimal solutions
/// </summary>
public class NearestNeighborOptimizer : IRouteOptimizer
{
    private readonly ILogger<NearestNeighborOptimizer> _logger;

    public OptimizationAlgorithm Algorithm => OptimizationAlgorithm.NearestNeighbor;

    public NearestNeighborOptimizer(ILogger<NearestNeighborOptimizer> logger)
    {
        _logger = logger;
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
        
        _logger.LogInformation("Starting nearest neighbor optimization for {JobCount} jobs", parameters.Jobs.Count);

        try
        {
            // Validate input
            if (!parameters.Jobs.Any())
            {
                return CreateEmptyResult(stopwatch.Elapsed);
            }

            // Get valid jobs (filter by skills if required)
            var validJobs = GetValidJobs(parameters);
            if (!validJobs.Any())
            {
                return CreateEmptyResult(stopwatch.Elapsed, new[] { "No valid jobs found for technician skills" });
            }

            // Calculate distance matrix if not provided
            var distanceMatrix = parameters.DistanceMatrix ?? 
                await BuildDistanceMatrixAsync(validJobs, parameters.StartLocation, cancellationToken);

            // Run nearest neighbor algorithm
            var optimizedStops = OptimizeWithNearestNeighbor(
                validJobs, 
                parameters, 
                distanceMatrix,
                cancellationToken);

            // Calculate route metrics
            var result = BuildOptimizationResult(
                optimizedStops, 
                parameters, 
                distanceMatrix, 
                stopwatch.Elapsed);

            _logger.LogInformation("Nearest neighbor optimization completed in {Duration}ms. " +
                "Distance: {Distance:F2}km, Duration: {Duration}",
                stopwatch.ElapsedMilliseconds, result.TotalDistanceKm, result.TotalDuration);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during nearest neighbor optimization");
            throw;
        }
    }

    private List<ServiceJob> GetValidJobs(RouteOptimizationParameters parameters)
    {
        var validJobs = parameters.Jobs.ToList();

        if (parameters.ValidateSkills)
        {
            // Filter jobs that require skills the technician doesn't have
            validJobs = validJobs.Where(job => 
                job.RequiredSkills.All(skill => parameters.Technician.Skills.Contains(skill)))
                .ToList();
        }

        return validJobs;
    }

    private List<OptimizedRouteStop> OptimizeWithNearestNeighbor(
        List<ServiceJob> jobs,
        RouteOptimizationParameters parameters,
        DistanceMatrix distanceMatrix,
        CancellationToken cancellationToken)
    {
        var result = new List<OptimizedRouteStop>();
        var remainingJobs = new List<ServiceJob>(jobs);
        var currentTime = parameters.Technician.WorkingHours.FirstOrDefault()?.StartTime ?? TimeSpan.FromHours(8);
        var currentDate = DateTime.Today.Add(currentTime);
        
        // Start from technician's location or first job
        var currentLocation = parameters.StartLocation ?? GetJobLocation(jobs.First());
        var currentLocationIndex = GetLocationIndex(currentLocation, distanceMatrix);

        int sequenceOrder = 1;

        while (remainingJobs.Any() && !cancellationToken.IsCancellationRequested)
        {
            // Find nearest unvisited job
            var nearestJob = FindNearestJob(
                currentLocationIndex, 
                remainingJobs, 
                distanceMatrix, 
                parameters.Objective);

            if (nearestJob == null) break;

            // Calculate travel details
            var jobLocationIndex = GetJobLocationIndex(nearestJob, distanceMatrix);
            var travelDistance = distanceMatrix.GetDistance(currentLocationIndex, jobLocationIndex);
            var travelTime = distanceMatrix.GetDuration(currentLocationIndex, jobLocationIndex);

            // Calculate arrival and departure times
            var estimatedArrival = currentDate.Add(travelTime);
            var estimatedDeparture = estimatedArrival.Add(nearestJob.EstimatedDuration);

            // Check constraints
            var violations = CheckConstraints(nearestJob, estimatedArrival, parameters);

            // Create optimized stop
            var optimizedStop = new OptimizedRouteStop
            {
                Job = nearestJob,
                SequenceOrder = sequenceOrder++,
                DistanceFromPreviousKm = travelDistance,
                TravelTimeFromPrevious = travelTime,
                EstimatedArrival = estimatedArrival,
                EstimatedDeparture = estimatedDeparture,
                HasConstraintViolations = violations.Any(),
                ConstraintViolations = violations
            };

            result.Add(optimizedStop);
            remainingJobs.Remove(nearestJob);

            // Update current position and time
            currentLocationIndex = jobLocationIndex;
            currentDate = estimatedDeparture;
        }

        return result;
    }

    private ServiceJob? FindNearestJob(
        int currentLocationIndex,
        List<ServiceJob> remainingJobs,
        DistanceMatrix distanceMatrix,
        OptimizationObjective objective)
    {
        ServiceJob? nearestJob = null;
        double bestCost = double.MaxValue;

        foreach (var job in remainingJobs)
        {
            var jobLocationIndex = GetJobLocationIndex(job, distanceMatrix);
            var cost = CalculateJobCost(currentLocationIndex, jobLocationIndex, job, distanceMatrix, objective);

            if (cost < bestCost)
            {
                bestCost = cost;
                nearestJob = job;
            }
        }

        return nearestJob;
    }

    private double CalculateJobCost(
        int fromLocationIndex,
        int toLocationIndex,
        ServiceJob job,
        DistanceMatrix distanceMatrix,
        OptimizationObjective objective)
    {
        return objective switch
        {
            OptimizationObjective.MinimizeDistance => distanceMatrix.GetDistance(fromLocationIndex, toLocationIndex),
            OptimizationObjective.MinimizeTime => distanceMatrix.GetDuration(fromLocationIndex, toLocationIndex).TotalMinutes,
            OptimizationObjective.MaximizeRevenue => -CalculateCostMetric(fromLocationIndex, toLocationIndex, job, distanceMatrix), // Negative to maximize
            _ => distanceMatrix.GetDistance(fromLocationIndex, toLocationIndex)
        };
    }

    private double CalculateCostMetric(
        int fromLocationIndex,
        int toLocationIndex,
        ServiceJob job,
        DistanceMatrix distanceMatrix)
    {
        // Simple cost model: distance cost + time cost - revenue benefit
        var distance = distanceMatrix.GetDistance(fromLocationIndex, toLocationIndex);
        var time = distanceMatrix.GetDuration(fromLocationIndex, toLocationIndex).TotalHours;
        
        const double costPerKm = 0.50; // $0.50 per km
        const double costPerHour = 25.0; // $25 per hour
        
        var travelCost = (distance * costPerKm) + (time * costPerHour);
        var revenueBenefit = (double)job.EstimatedRevenue * 0.1; // 10% of revenue as benefit
        
        return travelCost - revenueBenefit;
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
            var preferredEnd = preferredStart.AddHours(2); // Assume 2-hour window

            if (estimatedArrival < preferredStart || estimatedArrival > preferredEnd)
            {
                violations.Add($"Arrival time {estimatedArrival:HH:mm} outside preferred window {preferredStart:HH:mm}-{preferredEnd:HH:mm}");
            }
        }

        // Check technician working hours
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
        // Build locations list
        var locations = new List<Coordinate>();
        
        if (startLocation != null)
        {
            locations.Add(startLocation);
        }

        locations.AddRange(jobs.Select(GetJobLocation));

        var size = locations.Count;
        var distances = new double[size, size];
        var durations = new int[size, size];

        // Calculate distances using haversine formula (as approximation)
        // In a real implementation, this would call OSRM or similar routing service
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
                    
                    // Estimate duration: assume average speed of 40 km/h in city
                    var estimatedHours = distance / 40.0;
                    durations[i, j] = (int)(estimatedHours * 3600); // Convert to seconds
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
        return 0; // Default to first location
    }

    private static int GetJobLocationIndex(ServiceJob job, DistanceMatrix matrix)
    {
        var jobLocation = GetJobLocation(job);
        return GetLocationIndex(jobLocation, matrix);
    }

    private RouteOptimizationResult BuildOptimizationResult(
        List<OptimizedRouteStop> optimizedStops,
        RouteOptimizationParameters parameters,
        DistanceMatrix distanceMatrix,
        TimeSpan optimizationTime)
    {
        var totalDistance = optimizedStops.Sum(s => s.DistanceFromPreviousKm);
        var totalDuration = optimizedStops.Sum(s => s.TravelTimeFromPrevious.Ticks) + 
            optimizedStops.Sum(s => s.Job.EstimatedDuration.Ticks);
        
        var totalCost = CalculateTotalCost(optimizedStops, parameters.Technician.HourlyRate);
        var violations = optimizedStops.SelectMany(s => s.ConstraintViolations).ToList();

        var metrics = new OptimizationMetrics
        {
            InitialCost = totalDistance, // Use distance as initial cost for nearest neighbor
            FinalCost = totalDistance,
            Evaluations = optimizedStops.Count,
            CostHistory = new[] { totalDistance }
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
            IsOptimal = false, // Nearest neighbor is not optimal
            ConstraintViolations = violations,
            Metrics = metrics
        };
    }

    private static decimal CalculateTotalCost(List<OptimizedRouteStop> stops, decimal hourlyRate)
    {
        var totalTimeHours = stops.Sum(s => s.TravelTimeFromPrevious.TotalHours + s.Job.EstimatedDuration.TotalHours);
        return (decimal)totalTimeHours * hourlyRate;
    }

    private RouteOptimizationResult CreateEmptyResult(TimeSpan optimizationTime, IReadOnlyList<string>? violations = null)
    {
        return new RouteOptimizationResult
        {
            OptimizedStops = new List<OptimizedRouteStop>(),
            TotalDistanceKm = 0,
            TotalDuration = TimeSpan.Zero,
            TotalCost = 0,
            Algorithm = Algorithm,
            OptimizationTime = optimizationTime,
            Iterations = 0,
            IsOptimal = false,
            ConstraintViolations = violations ?? new List<string>()
        };
    }
}
