using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FieldOpsOptimizer.Infrastructure.Services;

/// <summary>
/// Implementation of metrics and analytics service
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly IRepository<ServiceJob> _jobRepository;
    private readonly IRepository<Technician> _technicianRepository;
    private readonly IRepository<Route> _routeRepository;
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(
        IRepository<ServiceJob> jobRepository,
        IRepository<Technician> technicianRepository,
        IRepository<Route> routeRepository,
        ILogger<MetricsService> logger)
    {
        _jobRepository = jobRepository;
        _technicianRepository = technicianRepository;
        _routeRepository = routeRepository;
        _logger = logger;
    }

    public async Task<BusinessMetrics> GetBusinessMetricsAsync(
        string tenantId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating business metrics for tenant {TenantId} from {FromDate} to {ToDate}",
            tenantId, fromDate, toDate);

        var jobs = (await _jobRepository.GetAllAsync(cancellationToken))
            .Where(j => j.TenantId == tenantId && j.ScheduledDate >= fromDate && j.ScheduledDate <= toDate)
            .ToList();

        var technicians = (await _technicianRepository.GetAllAsync(cancellationToken))
            .Where(t => t.TenantId == tenantId)
            .ToList();

        var completedJobs = jobs.Where(j => j.Status == JobStatus.Completed).ToList();

        return new BusinessMetrics
        {
            PeriodStart = fromDate,
            PeriodEnd = toDate,
            TotalJobs = jobs.Count,
            CompletedJobs = completedJobs.Count,
            PendingJobs = jobs.Count(j => j.Status == JobStatus.Scheduled || j.Status == JobStatus.InProgress),
            CancelledJobs = jobs.Count(j => j.Status == JobStatus.Cancelled),
            CompletionRate = jobs.Count > 0 ? (double)completedJobs.Count / jobs.Count * 100 : 0,
            TotalRevenue = completedJobs.Sum(j => j.EstimatedRevenue),
            AverageJobValue = completedJobs.Any() ? completedJobs.Average(j => j.EstimatedRevenue) : 0,
            AverageJobDurationHours = jobs.Where(j => j.EstimatedDuration != TimeSpan.Zero).Average(j => j.EstimatedDuration.TotalHours),
            ActiveTechnicians = technicians.Count(t => t.Status == TechnicianStatus.Active),
            AverageUtilizationRate = CalculateAverageUtilizationRate(technicians, jobs, fromDate, toDate)
        };
    }

    public async Task<TechnicianMetrics> GetTechnicianMetricsAsync(
        Guid technicianId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating metrics for technician {TechnicianId} from {FromDate} to {ToDate}",
            technicianId, fromDate, toDate);

        var technician = await _technicianRepository.GetByIdAsync(technicianId, cancellationToken);
        if (technician == null)
        {
            throw new ArgumentException($"Technician with ID {technicianId} not found");
        }

        var jobs = (await _jobRepository.GetAllAsync(cancellationToken))
            .Where(j => j.AssignedTechnicianId == technicianId && j.ScheduledDate >= fromDate && j.ScheduledDate <= toDate)
            .ToList();

        var completedJobs = jobs.Where(j => j.Status == JobStatus.Completed).ToList();

        return new TechnicianMetrics
        {
            TechnicianId = technicianId,
            TechnicianName = technician.FullName,
            JobsCompleted = completedJobs.Count,
            JobsCancelled = jobs.Count(j => j.Status == JobStatus.Cancelled),
            CompletionRate = jobs.Count > 0 ? (double)completedJobs.Count / jobs.Count * 100 : 0,
            AverageJobDuration = jobs.Where(j => j.EstimatedDuration != TimeSpan.Zero).Average(j => j.EstimatedDuration.TotalHours),
            TotalRevenue = completedJobs.Sum(j => j.EstimatedRevenue),
            UtilizationRate = CalculateUtilizationRate(technician, jobs, fromDate, toDate),
            CustomerSatisfactionScore = 0 // TODO: Implement customer satisfaction tracking
        };
    }

    public async Task<RouteMetrics> GetRouteMetricsAsync(
        string tenantId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating route metrics for tenant {TenantId} from {FromDate} to {ToDate}",
            tenantId, fromDate, toDate);

        var routes = (await _routeRepository.GetAllAsync(cancellationToken))
            .Where(r => r.TenantId == tenantId && r.CreatedAt >= fromDate && r.CreatedAt <= toDate)
            .ToList();

        return new RouteMetrics
        {
            TotalRoutes = routes.Count,
            OptimizedRoutes = routes.Count(r => r.IsOptimized),
            AverageTravelTime = routes.Where(r => r.TotalTravelTime.HasValue).Average(r => r.TotalTravelTime!.Value.TotalHours),
            AverageDistance = routes.Where(r => r.TotalDistance.HasValue).Average(r => r.TotalDistance!.Value),
            TotalFuelSavings = routes.Sum(r => r.EstimatedFuelSavings ?? 0),
            TotalTimeSavings = routes.Where(r => r.EstimatedTimeSavings.HasValue).Sum(r => r.EstimatedTimeSavings!.Value.TotalHours),
            AlgorithmUsage = routes
                .Where(r => !string.IsNullOrEmpty(r.OptimizationAlgorithm))
                .GroupBy(r => r.OptimizationAlgorithm!)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public async Task<JobMetrics> GetJobMetricsAsync(
        string tenantId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating job metrics for tenant {TenantId} from {FromDate} to {ToDate}",
            tenantId, fromDate, toDate);

        var jobs = (await _jobRepository.GetAllAsync(cancellationToken))
            .Where(j => j.TenantId == tenantId && j.ScheduledDate >= fromDate && j.ScheduledDate <= toDate)
            .ToList();

        var durations = jobs.Where(j => j.EstimatedDuration != TimeSpan.Zero)
            .Select(j => j.EstimatedDuration.TotalHours)
            .OrderBy(d => d)
            .ToArray();

        return new JobMetrics
        {
            StatusBreakdown = jobs.GroupBy(j => j.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            PriorityBreakdown = jobs.GroupBy(j => j.Priority.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            TypeBreakdown = new Dictionary<string, int>(), // TODO: Add job type property to ServiceJob
            AverageDuration = durations.Any() ? durations.Average() : 0,
            MedianDuration = durations.Any() ? CalculateMedian(durations) : 0,
            TotalValue = jobs.Sum(j => j.EstimatedRevenue),
            AverageJobValue = jobs.Any() ? jobs.Average(j => j.EstimatedRevenue) : 0
        };
    }

    private static double CalculateAverageUtilizationRate(
        IEnumerable<Technician> technicians,
        IEnumerable<ServiceJob> jobs,
        DateTime fromDate,
        DateTime toDate)
    {
        var utilizationRates = technicians.Select(t => CalculateUtilizationRate(t, jobs, fromDate, toDate));
        return utilizationRates.Any() ? utilizationRates.Average() : 0;
    }

    private static double CalculateUtilizationRate(
        Technician technician,
        IEnumerable<ServiceJob> jobs,
        DateTime fromDate,
        DateTime toDate)
    {
        var technicianJobs = jobs.Where(j => j.AssignedTechnicianId == technician.Id).ToList();
        if (!technicianJobs.Any())
            return 0;

        var totalWorkingHours = CalculateTotalWorkingHours(technician, fromDate, toDate);
        if (totalWorkingHours == 0)
            return 0;

        var scheduledHours = technicianJobs.Sum(j => j.EstimatedDuration.TotalHours);
        return Math.Min(100, (scheduledHours / totalWorkingHours) * 100);
    }

    private static double CalculateTotalWorkingHours(Technician technician, DateTime fromDate, DateTime toDate)
    {
        var totalHours = 0.0;
        var currentDate = fromDate.Date;

        while (currentDate <= toDate.Date)
        {
            var dayOfWeek = currentDate.DayOfWeek;
            var workingHours = technician.WorkingHours
                .FirstOrDefault(wh => wh.DayOfWeek == dayOfWeek);

            if (workingHours != null)
            {
                totalHours += (workingHours.EndTime - workingHours.StartTime).TotalHours;
            }

            currentDate = currentDate.AddDays(1);
        }

        return totalHours;
    }

    private static double CalculateMedian(double[] sortedValues)
    {
        if (sortedValues.Length == 0) return 0;

        if (sortedValues.Length % 2 == 0)
        {
            return (sortedValues[sortedValues.Length / 2 - 1] + sortedValues[sortedValues.Length / 2]) / 2;
        }
        
        return sortedValues[sortedValues.Length / 2];
    }
}
