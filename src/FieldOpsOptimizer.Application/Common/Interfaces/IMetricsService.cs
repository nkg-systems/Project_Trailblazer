namespace FieldOpsOptimizer.Application.Common.Interfaces;

/// <summary>
/// Service for business metrics and analytics
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Gets business metrics for a specific time period
    /// </summary>
    Task<BusinessMetrics> GetBusinessMetricsAsync(
        string tenantId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets technician performance metrics
    /// </summary>
    Task<TechnicianMetrics> GetTechnicianMetricsAsync(
        Guid technicianId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets route optimization effectiveness metrics
    /// </summary>
    Task<RouteMetrics> GetRouteMetricsAsync(
        string tenantId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets job completion metrics
    /// </summary>
    Task<JobMetrics> GetJobMetricsAsync(
        string tenantId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Business metrics for a tenant
/// </summary>
public class BusinessMetrics
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int PendingJobs { get; set; }
    public int CancelledJobs { get; set; }
    public double CompletionRate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageJobValue { get; set; }
    public double AverageJobDurationHours { get; set; }
    public int ActiveTechnicians { get; set; }
    public double AverageUtilizationRate { get; set; }
}

/// <summary>
/// Performance metrics for a technician
/// </summary>
public class TechnicianMetrics
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public int JobsCompleted { get; set; }
    public int JobsCancelled { get; set; }
    public double CompletionRate { get; set; }
    public double AverageJobDuration { get; set; }
    public decimal TotalRevenue { get; set; }
    public double UtilizationRate { get; set; }
    public double CustomerSatisfactionScore { get; set; }
}

/// <summary>
/// Route optimization metrics
/// </summary>
public class RouteMetrics
{
    public int TotalRoutes { get; set; }
    public int OptimizedRoutes { get; set; }
    public double AverageTravelTime { get; set; }
    public double AverageDistance { get; set; }
    public double TotalFuelSavings { get; set; }
    public double TotalTimeSavings { get; set; }
    public Dictionary<string, int> AlgorithmUsage { get; set; } = new();
}

/// <summary>
/// Job completion and status metrics
/// </summary>
public class JobMetrics
{
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();
    public Dictionary<string, int> PriorityBreakdown { get; set; } = new();
    public Dictionary<string, int> TypeBreakdown { get; set; } = new();
    public double AverageDuration { get; set; }
    public double MedianDuration { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageJobValue { get; set; }
}
