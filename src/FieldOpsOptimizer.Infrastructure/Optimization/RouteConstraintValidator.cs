using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Services;
using FieldOpsOptimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FieldOpsOptimizer.Infrastructure.Optimization;

/// <summary>
/// Service for validating route constraints
/// </summary>
public interface IRouteConstraintValidator
{
    /// <summary>
    /// Validates constraints for a complete route
    /// </summary>
    RouteConstraintValidationResult ValidateRoute(
        IReadOnlyList<OptimizedRouteStop> stops,
        RouteOptimizationParameters parameters);

    /// <summary>
    /// Validates constraints for a single stop
    /// </summary>
    List<string> ValidateStop(
        ServiceJob job,
        DateTime estimatedArrival,
        DateTime estimatedDeparture,
        RouteOptimizationParameters parameters);

    /// <summary>
    /// Checks if a technician can perform a job based on skills
    /// </summary>
    bool CanTechnicianPerformJob(Technician technician, ServiceJob job);

    /// <summary>
    /// Calculates penalty score for constraint violations
    /// </summary>
    double CalculateConstraintPenalty(IReadOnlyList<string> violations);
}

public class RouteConstraintValidator : IRouteConstraintValidator
{
    private readonly ILogger<RouteConstraintValidator> _logger;

    public RouteConstraintValidator(ILogger<RouteConstraintValidator> logger)
    {
        _logger = logger;
    }

    public RouteConstraintValidationResult ValidateRoute(
        IReadOnlyList<OptimizedRouteStop> stops,
        RouteOptimizationParameters parameters)
    {
        var allViolations = new List<string>();
        var violationsByStop = new Dictionary<int, List<string>>();
        var totalPenalty = 0.0;

        foreach (var stop in stops)
        {
            var stopViolations = ValidateStop(
                stop.Job,
                stop.EstimatedArrival,
                stop.EstimatedDeparture,
                parameters);

            if (stopViolations.Any())
            {
                violationsByStop[stop.SequenceOrder] = stopViolations;
                allViolations.AddRange(stopViolations);
                totalPenalty += CalculateConstraintPenalty(stopViolations);
            }
        }

        // Check route-level constraints
        var routeViolations = ValidateRouteLevel(stops, parameters);
        allViolations.AddRange(routeViolations);
        totalPenalty += CalculateConstraintPenalty(routeViolations);

        return new RouteConstraintValidationResult
        {
            IsValid = !allViolations.Any(),
            AllViolations = allViolations,
            ViolationsByStop = violationsByStop,
            TotalPenalty = totalPenalty,
            ViolationCount = allViolations.Count
        };
    }

    public List<string> ValidateStop(
        ServiceJob job,
        DateTime estimatedArrival,
        DateTime estimatedDeparture,
        RouteOptimizationParameters parameters)
    {
        var violations = new List<string>();

        // Time window constraints
        if (parameters.RespectTimeWindows && job.PreferredTimeWindow.HasValue)
        {
            var preferredStart = job.ScheduledDate.Date.Add(job.PreferredTimeWindow.Value);
            var preferredEnd = preferredStart.AddHours(2); // Assume 2-hour window

            if (estimatedArrival < preferredStart)
            {
                violations.Add($"Job {job.JobNumber}: Arrival {estimatedArrival:HH:mm} is {(preferredStart - estimatedArrival).TotalMinutes:F0} minutes before preferred time {preferredStart:HH:mm}");
            }
            else if (estimatedArrival > preferredEnd)
            {
                violations.Add($"Job {job.JobNumber}: Arrival {estimatedArrival:HH:mm} is {(estimatedArrival - preferredEnd).TotalMinutes:F0} minutes after preferred window ends at {preferredEnd:HH:mm}");
            }
        }

        // Working hours constraints
        var arrivalTime = estimatedArrival.TimeOfDay;
        var departureTime = estimatedDeparture.TimeOfDay;
        var workingHours = parameters.Technician.WorkingHours.FirstOrDefault();

        if (workingHours != null)
        {
            if (arrivalTime < workingHours.StartTime)
            {
                violations.Add($"Job {job.JobNumber}: Arrival {arrivalTime} is before working hours start at {workingHours.StartTime}");
            }

            if (departureTime > workingHours.EndTime)
            {
                violations.Add($"Job {job.JobNumber}: Departure {departureTime} is after working hours end at {workingHours.EndTime}");
            }
        }

        // Skill constraints
        if (parameters.ValidateSkills && !CanTechnicianPerformJob(parameters.Technician, job))
        {
            var missingSkills = job.RequiredSkills.Except(parameters.Technician.Skills);
            violations.Add($"Job {job.JobNumber}: Technician missing required skills: {string.Join(", ", missingSkills)}");
        }

        // Date constraints
        if (estimatedArrival.Date != job.ScheduledDate.Date)
        {
            violations.Add($"Job {job.JobNumber}: Scheduled for {job.ScheduledDate:yyyy-MM-dd} but arriving on {estimatedArrival:yyyy-MM-dd}");
        }

        return violations;
    }

    public bool CanTechnicianPerformJob(Technician technician, ServiceJob job)
    {
        // Check if technician has all required skills
        return job.RequiredSkills.All(skill => technician.Skills.Contains(skill));
    }

    public double CalculateConstraintPenalty(IReadOnlyList<string> violations)
    {
        if (!violations.Any()) return 0.0;

        double penalty = 0.0;

        foreach (var violation in violations)
        {
            // Different penalty weights for different types of violations
            if (violation.Contains("working hours"))
            {
                penalty += 100.0; // High penalty for working hours violations
            }
            else if (violation.Contains("preferred time"))
            {
                penalty += 50.0; // Medium penalty for time window violations
            }
            else if (violation.Contains("missing required skills"))
            {
                penalty += 200.0; // Very high penalty for skill violations
            }
            else if (violation.Contains("Scheduled for"))
            {
                penalty += 500.0; // Extremely high penalty for wrong date
            }
            else
            {
                penalty += 25.0; // Default penalty
            }
        }

        return penalty;
    }

    private List<string> ValidateRouteLevel(
        IReadOnlyList<OptimizedRouteStop> stops,
        RouteOptimizationParameters parameters)
    {
        var violations = new List<string>();

        if (!stops.Any()) return violations;

        // Check total route duration doesn't exceed maximum work day
        var totalDuration = stops.Last().EstimatedDeparture - stops.First().EstimatedArrival;
        var maxWorkDayHours = 12; // Default maximum work day

        if (totalDuration.TotalHours > maxWorkDayHours)
        {
            violations.Add($"Route duration {totalDuration.TotalHours:F1} hours exceeds maximum work day of {maxWorkDayHours} hours");
        }

        // Check for overlapping jobs (same location, same time)
        var jobsByLocation = stops
            .GroupBy(s => s.Job.ServiceAddress.FormattedAddress)
            .Where(g => g.Count() > 1);

        foreach (var locationGroup in jobsByLocation)
        {
            var sortedStops = locationGroup.OrderBy(s => s.EstimatedArrival).ToList();
            for (int i = 0; i < sortedStops.Count - 1; i++)
            {
                if (sortedStops[i].EstimatedDeparture > sortedStops[i + 1].EstimatedArrival)
                {
                    violations.Add($"Overlapping jobs at {locationGroup.Key}: " +
                        $"Job {sortedStops[i].Job.JobNumber} ends at {sortedStops[i].EstimatedDeparture:HH:mm}, " +
                        $"Job {sortedStops[i + 1].Job.JobNumber} starts at {sortedStops[i + 1].EstimatedArrival:HH:mm}");
                }
            }
        }

        return violations;
    }
}

/// <summary>
/// Result of route constraint validation
/// </summary>
public record RouteConstraintValidationResult
{
    /// <summary>
    /// Whether the route satisfies all constraints
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// All constraint violations found
    /// </summary>
    public IReadOnlyList<string> AllViolations { get; init; } = new List<string>();

    /// <summary>
    /// Violations grouped by stop sequence order
    /// </summary>
    public Dictionary<int, List<string>> ViolationsByStop { get; init; } = new();

    /// <summary>
    /// Total penalty score for all violations
    /// </summary>
    public double TotalPenalty { get; init; }

    /// <summary>
    /// Total number of violations
    /// </summary>
    public int ViolationCount { get; init; }

    /// <summary>
    /// Whether the route has hard constraint violations (skills, dates)
    /// </summary>
    public bool HasHardViolations => AllViolations.Any(v => 
        v.Contains("missing required skills") || v.Contains("Scheduled for"));

    /// <summary>
    /// Whether the route has soft constraint violations (time windows, working hours)
    /// </summary>
    public bool HasSoftViolations => AllViolations.Any(v => 
        v.Contains("preferred time") || v.Contains("working hours"));
}
