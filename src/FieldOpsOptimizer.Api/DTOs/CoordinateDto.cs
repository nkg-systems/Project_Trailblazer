using System.ComponentModel.DataAnnotations;

namespace FieldOpsOptimizer.Api.DTOs;

/// <summary>
/// Business metrics data
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
