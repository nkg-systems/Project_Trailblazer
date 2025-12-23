using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FieldOpsOptimizer.Infrastructure.Optimization;

/// <summary>
/// Genetic algorithm for route optimization
/// Uses population-based search with crossover and mutation
/// </summary>
public class GeneticOptimizer : IRouteOptimizer
{
    private readonly ILogger<GeneticOptimizer> _logger;
    private readonly Random _random = new();

    public OptimizationAlgorithm Algorithm => OptimizationAlgorithm.Genetic;

    // Genetic algorithm parameters
    private const int PopulationSize = 50;
    private const double MutationRate = 0.02;
    private const double CrossoverRate = 0.8;
    private const int EliteSize = 5;
    private const int MaxGenerations = 100;

    public GeneticOptimizer(ILogger<GeneticOptimizer> logger)
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
        
        _logger.LogInformation("Starting genetic algorithm optimization for {JobCount} jobs", parameters.Jobs.Count);

        try
        {
            if (!parameters.Jobs.Any())
            {
                return CreateEmptyResult(stopwatch.Elapsed);
            }

            var validJobs = GetValidJobs(parameters);
            if (!validJobs.Any())
            {
                return CreateEmptyResult(stopwatch.Elapsed, new[] { "No valid jobs found for technician skills" });
            }

            var distanceMatrix = parameters.DistanceMatrix ?? 
                await BuildDistanceMatrixAsync(validJobs, parameters.StartLocation, cancellationToken);

            // Run genetic algorithm
            var (bestRoute, metrics) = RunGeneticAlgorithm(
                validJobs,
                parameters,
                distanceMatrix,
                cancellationToken);

            // Build optimized stops
            var optimizedStops = BuildOptimizedStops(bestRoute, parameters, distanceMatrix);

            var result = BuildOptimizationResult(
                optimizedStops,
                parameters,
                distanceMatrix,
                stopwatch.Elapsed,
                metrics);

            _logger.LogInformation("Genetic algorithm completed in {Duration}ms. " +
                "Distance: {Distance:F2}km (improvement: {Improvement:F1}%)",
                stopwatch.ElapsedMilliseconds, result.TotalDistanceKm, result.Metrics.ImprovementPercentage);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during genetic algorithm optimization");
            throw;
        }
    }

    private (List<ServiceJob> bestRoute, OptimizationMetrics metrics) RunGeneticAlgorithm(
        List<ServiceJob> jobs,
        RouteOptimizationParameters parameters,
        DistanceMatrix distanceMatrix,
        CancellationToken cancellationToken)
    {
        // Initialize population
        var population = InitializePopulation(jobs, PopulationSize);
        var costHistory = new List<double>();
        var evaluations = 0;

        var bestRoute = population.First();
        var bestCost = CalculateRouteCost(bestRoute, distanceMatrix, parameters.Objective);
        var initialCost = bestCost;
        costHistory.Add(bestCost);

        _logger.LogDebug("Initial population created. Best cost: {Cost:F2}", bestCost);

        for (int generation = 0; generation < MaxGenerations && !cancellationToken.IsCancellationRequested; generation++)
        {
            // Evaluate fitness for all individuals
            var fitness = population.Select(route => 1.0 / (1.0 + CalculateRouteCost(route, distanceMatrix, parameters.Objective))).ToArray();
            evaluations += population.Count;

            // Selection, crossover, and mutation
            var newPopulation = new List<List<ServiceJob>>();

            // Keep elite individuals
            var sortedIndices = fitness
                .Select((f, index) => new { Fitness = f, Index = index })
                .OrderByDescending(x => x.Fitness)
                .Select(x => x.Index)
                .ToArray();

            for (int i = 0; i < EliteSize; i++)
            {
                newPopulation.Add(new List<ServiceJob>(population[sortedIndices[i]]));
            }

            // Generate offspring
            while (newPopulation.Count < PopulationSize)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var parent1 = TournamentSelection(population, fitness);
                var parent2 = TournamentSelection(population, fitness);

                var (child1, child2) = _random.NextDouble() < CrossoverRate 
                    ? OrderCrossover(parent1, parent2)
                    : (new List<ServiceJob>(parent1), new List<ServiceJob>(parent2));

                // Apply mutation
                if (_random.NextDouble() < MutationRate)
                {
                    SwapMutation(child1);
                }
                if (_random.NextDouble() < MutationRate)
                {
                    SwapMutation(child2);
                }

                newPopulation.Add(child1);
                if (newPopulation.Count < PopulationSize)
                {
                    newPopulation.Add(child2);
                }
            }

            population = newPopulation;

            // Update best solution
            foreach (var route in population)
            {
                var cost = CalculateRouteCost(route, distanceMatrix, parameters.Objective);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestRoute = new List<ServiceJob>(route);
                    costHistory.Add(bestCost);
                }
            }

            if (generation % 10 == 0)
            {
                _logger.LogDebug("Generation {Generation}: best cost {Cost:F2}", generation, bestCost);
            }
        }

        var metrics = new OptimizationMetrics
        {
            InitialCost = initialCost,
            FinalCost = bestCost,
            Evaluations = evaluations,
            CostHistory = costHistory
        };

        return (bestRoute, metrics);
    }

    private List<List<ServiceJob>> InitializePopulation(List<ServiceJob> jobs, int populationSize)
    {
        var population = new List<List<ServiceJob>>();

        // First individual: original order
        population.Add(new List<ServiceJob>(jobs));

        // Generate random permutations
        for (int i = 1; i < populationSize; i++)
        {
            var individual = new List<ServiceJob>(jobs);
            
            // Shuffle using Fisher-Yates algorithm
            for (int j = individual.Count - 1; j > 0; j--)
            {
                int k = _random.Next(j + 1);
                (individual[j], individual[k]) = (individual[k], individual[j]);
            }
            
            population.Add(individual);
        }

        return population;
    }

    private List<ServiceJob> TournamentSelection(List<List<ServiceJob>> population, double[] fitness)
    {
        const int tournamentSize = 3;
        var best = _random.Next(population.Count);
        var bestFitness = fitness[best];

        for (int i = 1; i < tournamentSize; i++)
        {
            var candidate = _random.Next(population.Count);
            if (fitness[candidate] > bestFitness)
            {
                best = candidate;
                bestFitness = fitness[candidate];
            }
        }

        return new List<ServiceJob>(population[best]);
    }

    private (List<ServiceJob> child1, List<ServiceJob> child2) OrderCrossover(
        List<ServiceJob> parent1, 
        List<ServiceJob> parent2)
    {
        var size = parent1.Count;
        var start = _random.Next(size);
        var end = _random.Next(start, size);

        var child1 = new ServiceJob?[size];
        var child2 = new ServiceJob?[size];

        // Copy substring from parent1 to child1 and parent2 to child2
        for (int i = start; i <= end; i++)
        {
            child1[i] = parent1[i];
            child2[i] = parent2[i];
        }

        // Fill remaining positions with jobs from other parent in order
        FillRemainingPositions(child1, parent2, start, end);
        FillRemainingPositions(child2, parent1, start, end);

        return (child1.Where(j => j != null).Select(j => j!).ToList(), child2.Where(j => j != null).Select(j => j!).ToList());
    }

    private static void FillRemainingPositions(ServiceJob?[] child, List<ServiceJob> otherParent, int start, int end)
    {
        var usedJobs = child.Where(j => j != null).ToHashSet();
        var remainingJobs = otherParent.Where(j => !usedJobs.Contains(j)).ToList();
        
        int jobIndex = 0;
        for (int i = 0; i < child.Length; i++)
        {
            if (child[i] == null && jobIndex < remainingJobs.Count)
            {
                child[i] = remainingJobs[jobIndex++];
            }
        }
    }

    private void SwapMutation(List<ServiceJob> route)
    {
        if (route.Count < 2) return;

        int i = _random.Next(route.Count);
        int j = _random.Next(route.Count);
        
        (route[i], route[j]) = (route[j], route[i]);
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
        
        return revenueBenefit - travelCost; // Revenue minus cost
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

    private List<ServiceJob> GetValidJobs(RouteOptimizationParameters parameters)
    {
        var validJobs = parameters.Jobs.ToList();

        if (parameters.ValidateSkills)
        {
            validJobs = validJobs.Where(job => 
                job.RequiredSkills.All(skill => parameters.Technician.Skills.Contains(skill)))
                .ToList();
        }

        return validJobs;
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
        OptimizationMetrics genetics)
    {
        var totalDistance = optimizedStops.Sum(s => s.DistanceFromPreviousKm);
        var totalDuration = optimizedStops.Sum(s => s.TravelTimeFromPrevious.Ticks) + 
            optimizedStops.Sum(s => s.Job.EstimatedDuration.Ticks);
        
        var totalCost = CalculateTotalCost(optimizedStops, parameters.Technician.HourlyRate);
        var violations = optimizedStops.SelectMany(s => s.ConstraintViolations).ToList();

        var metrics = new OptimizationMetrics
        {
            InitialCost = genetics.InitialCost,
            FinalCost = totalDistance,
            Evaluations = genetics.Evaluations,
            CostHistory = genetics.CostHistory,
            AdditionalMetrics = new Dictionary<string, object>
            {
                ["PopulationSize"] = PopulationSize,
                ["MutationRate"] = MutationRate,
                ["CrossoverRate"] = CrossoverRate,
                ["MaxGenerations"] = MaxGenerations
            }
        };

        return new RouteOptimizationResult
        {
            OptimizedStops = optimizedStops,
            TotalDistanceKm = totalDistance,
            TotalDuration = TimeSpan.FromTicks(totalDuration),
            TotalCost = totalCost,
            Algorithm = Algorithm,
            OptimizationTime = optimizationTime,
            Iterations = MaxGenerations,
            IsOptimal = false, // Genetic algorithm is heuristic
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
