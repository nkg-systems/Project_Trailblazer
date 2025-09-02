using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Api.DTOs;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// Controller for generating reports and analytics
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(ApplicationDbContext context, IMapper mapper, ILogger<ReportsController> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Get performance dashboard data
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<DashboardReportDto>>> GetDashboard(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? tenantId)
    {
        try
        {
            var dateStart = startDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var dateEnd = endDate ?? DateTime.UtcNow.Date;

            var query = _context.ServiceJobs.AsQueryable();
            
            if (tenantId.HasValue)
                query = query.Where(j => j.TenantId == tenantId.Value);

            var jobs = await query
                .Where(j => j.ScheduledDate >= dateStart && j.ScheduledDate <= dateEnd)
                .Include(j => j.Technician)
                .ToListAsync();

            var technicians = await _context.Technicians
                .Where(t => !tenantId.HasValue || t.TenantId == tenantId.Value)
                .Include(t => t.ServiceJobs.Where(j => j.ScheduledDate >= dateStart && j.ScheduledDate <= dateEnd))
                .ToListAsync();

            var dashboard = new DashboardReportDto
            {
                ReportPeriod = $"{dateStart:yyyy-MM-dd} to {dateEnd:yyyy-MM-dd}",
                GeneratedAt = DateTime.UtcNow,
                
                // Job metrics
                TotalJobs = jobs.Count,
                CompletedJobs = jobs.Count(j => j.Status == ServiceJobStatus.Completed),
                PendingJobs = jobs.Count(j => j.Status == ServiceJobStatus.Scheduled || j.Status == ServiceJobStatus.InProgress),
                CancelledJobs = jobs.Count(j => j.Status == ServiceJobStatus.Cancelled),
                
                // Performance metrics
                CompletionRate = jobs.Count > 0 ? (double)jobs.Count(j => j.Status == ServiceJobStatus.Completed) / jobs.Count * 100 : 0,
                AverageJobDuration = jobs.Where(j => j.EstimatedDuration.HasValue).Average(j => j.EstimatedDuration!.Value.TotalHours),
                
                // Technician metrics
                ActiveTechnicians = technicians.Count(t => t.Status == TechnicianStatus.Active),
                TotalTechnicians = technicians.Count,
                AverageUtilizationRate = CalculateAverageUtilization(technicians, dateStart, dateEnd),
                
                // Revenue metrics (if costs are tracked)
                TotalRevenue = jobs.Where(j => j.Status == ServiceJobStatus.Completed).Sum(j => j.ActualCost ?? 0),
                AverageJobValue = jobs.Where(j => j.ActualCost.HasValue && j.Status == ServiceJobStatus.Completed).Average(j => j.ActualCost ?? 0),
                
                // Recent activity
                RecentJobs = jobs.OrderByDescending(j => j.UpdatedAt).Take(5).Select(j => new RecentJobDto
                {
                    JobId = j.Id,
                    JobNumber = j.JobNumber,
                    Title = j.Title,
                    Status = j.Status.ToString(),
                    TechnicianName = j.Technician?.Name ?? "Unassigned",
                    ScheduledDate = j.ScheduledDate,
                    UpdatedAt = j.UpdatedAt
                }).ToList()
            };

            return Ok(new ApiResponse<DashboardReportDto>
            {
                Success = true,
                Data = dashboard,
                Message = "Dashboard data retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dashboard report");
            return StatusCode(500, new ApiResponse<DashboardReportDto>
            {
                Success = false,
                Message = "An error occurred while generating dashboard data",
                Errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get technician performance report
    /// </summary>
    [HttpGet("technician-performance")]
    public async Task<ActionResult<ApiResponse<List<TechnicianPerformanceDto>>>> GetTechnicianPerformance(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? technicianId,
        [FromQuery] Guid? tenantId)
    {
        try
        {
            var dateStart = startDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var dateEnd = endDate ?? DateTime.UtcNow.Date;

            var query = _context.Technicians
                .Include(t => t.ServiceJobs.Where(j => 
                    j.ScheduledDate >= dateStart && j.ScheduledDate <= dateEnd))
                .AsQueryable();

            if (technicianId.HasValue)
                query = query.Where(t => t.Id == technicianId.Value);

            if (tenantId.HasValue)
                query = query.Where(t => t.TenantId == tenantId.Value);

            var technicians = await query.ToListAsync();

            var performance = technicians.Select(t => new TechnicianPerformanceDto
            {
                TechnicianId = t.Id,
                TechnicianName = t.Name,
                EmployeeId = t.EmployeeId,
                Status = t.Status.ToString(),
                TotalJobs = t.ServiceJobs.Count,
                CompletedJobs = t.ServiceJobs.Count(j => j.Status == ServiceJobStatus.Completed),
                CancelledJobs = t.ServiceJobs.Count(j => j.Status == ServiceJobStatus.Cancelled),
                CompletionRate = t.ServiceJobs.Count > 0 ? 
                    (double)t.ServiceJobs.Count(j => j.Status == ServiceJobStatus.Completed) / t.ServiceJobs.Count * 100 : 0,
                AverageJobDuration = t.ServiceJobs.Where(j => j.EstimatedDuration.HasValue)
                    .Average(j => j.EstimatedDuration!.Value.TotalHours),
                TotalRevenue = t.ServiceJobs.Where(j => j.Status == ServiceJobStatus.Completed)
                    .Sum(j => j.ActualCost ?? 0),
                UtilizationRate = CalculateUtilizationRate(t, dateStart, dateEnd),
                SkillsCount = t.Skills.Count,
                BaseLocation = t.BaseLocation ?? "Not specified",
                HourlyRate = t.HourlyRate
            }).ToList();

            return Ok(new ApiResponse<List<TechnicianPerformanceDto>>
            {
                Success = true,
                Data = performance,
                Message = $"Retrieved performance data for {performance.Count} technicians"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating technician performance report");
            return StatusCode(500, new ApiResponse<List<TechnicianPerformanceDto>>
            {
                Success = false,
                Message = "An error occurred while generating performance report",
                Errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get job analytics and trends
    /// </summary>
    [HttpGet("job-analytics")]
    public async Task<ActionResult<ApiResponse<JobAnalyticsDto>>> GetJobAnalytics(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? tenantId)
    {
        try
        {
            var dateStart = startDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var dateEnd = endDate ?? DateTime.UtcNow.Date;

            var query = _context.ServiceJobs.AsQueryable();
            
            if (tenantId.HasValue)
                query = query.Where(j => j.TenantId == tenantId.Value);

            var jobs = await query
                .Where(j => j.ScheduledDate >= dateStart && j.ScheduledDate <= dateEnd)
                .ToListAsync();

            var analytics = new JobAnalyticsDto
            {
                ReportPeriod = $"{dateStart:yyyy-MM-dd} to {dateEnd:yyyy-MM-dd}",
                TotalJobs = jobs.Count,
                
                // Status distribution
                StatusBreakdown = Enum.GetValues<ServiceJobStatus>()
                    .ToDictionary(s => s.ToString(), s => jobs.Count(j => j.Status == s)),
                
                // Priority distribution
                PriorityBreakdown = Enum.GetValues<JobPriority>()
                    .ToDictionary(p => p.ToString(), p => jobs.Count(j => j.Priority == p)),
                
                // Job type distribution
                JobTypeBreakdown = jobs.GroupBy(j => j.JobType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                
                // Duration analytics
                AverageDuration = jobs.Where(j => j.EstimatedDuration.HasValue)
                    .Average(j => j.EstimatedDuration!.Value.TotalHours),
                MedianDuration = CalculateMedian(jobs.Where(j => j.EstimatedDuration.HasValue)
                    .Select(j => j.EstimatedDuration!.Value.TotalHours)),
                
                // Cost analytics
                TotalValue = jobs.Sum(j => j.EstimatedCost ?? 0),
                AverageJobValue = jobs.Where(j => j.EstimatedCost.HasValue)
                    .Average(j => j.EstimatedCost!.Value),
                
                // Daily trends
                DailyTrends = jobs.GroupBy(j => j.ScheduledDate.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new DailyTrendDto
                    {
                        Date = g.Key,
                        JobCount = g.Count(),
                        CompletedCount = g.Count(j => j.Status == ServiceJobStatus.Completed),
                        Revenue = g.Where(j => j.Status == ServiceJobStatus.Completed).Sum(j => j.ActualCost ?? 0)
                    }).ToList(),
                
                // Geographic distribution
                LocationAnalysis = jobs.Where(j => !string.IsNullOrEmpty(j.ServiceAddress.City))
                    .GroupBy(j => j.ServiceAddress.City)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return Ok(new ApiResponse<JobAnalyticsDto>
            {
                Success = true,
                Data = analytics,
                Message = "Job analytics retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating job analytics");
            return StatusCode(500, new ApiResponse<JobAnalyticsDto>
            {
                Success = false,
                Message = "An error occurred while generating job analytics",
                Errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get route optimization effectiveness report
    /// </summary>
    [HttpGet("route-optimization")]
    public async Task<ActionResult<ApiResponse<RouteOptimizationReportDto>>> GetRouteOptimizationReport(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? tenantId)
    {
        try
        {
            var dateStart = startDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var dateEnd = endDate ?? DateTime.UtcNow.Date;

            var query = _context.Routes.AsQueryable();
            
            if (tenantId.HasValue)
                query = query.Where(r => r.TenantId == tenantId.Value);

            var routes = await query
                .Where(r => r.Date >= dateStart && r.Date <= dateEnd)
                .Include(r => r.RouteStops)
                .Include(r => r.Technician)
                .ToListAsync();

            var report = new RouteOptimizationReportDto
            {
                ReportPeriod = $"{dateStart:yyyy-MM-dd} to {dateEnd:yyyy-MM-dd}",
                TotalRoutes = routes.Count,
                OptimizedRoutes = routes.Count(r => r.IsOptimized),
                AverageTravelTime = routes.Average(r => r.TotalTravelTime?.TotalHours ?? 0),
                AverageDistance = routes.Average(r => r.TotalDistance ?? 0),
                TotalFuelSavings = routes.Sum(r => r.EstimatedFuelSavings ?? 0),
                TotalTimeSavings = routes.Sum(r => r.EstimatedTimeSavings?.TotalHours ?? 0),
                
                // Algorithm effectiveness
                AlgorithmBreakdown = routes.Where(r => !string.IsNullOrEmpty(r.OptimizationAlgorithm))
                    .GroupBy(r => r.OptimizationAlgorithm!)
                    .ToDictionary(g => g.Key, g => new AlgorithmStatsDto
                    {
                        Count = g.Count(),
                        AverageTravelTime = g.Average(r => r.TotalTravelTime?.TotalHours ?? 0),
                        AverageDistance = g.Average(r => r.TotalDistance ?? 0),
                        AverageSavings = g.Average(r => r.EstimatedTimeSavings?.TotalHours ?? 0)
                    }),
                
                // Daily optimization trends
                DailyOptimization = routes.GroupBy(r => r.Date.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new OptimizationTrendDto
                    {
                        Date = g.Key,
                        RoutesCreated = g.Count(),
                        RoutesOptimized = g.Count(r => r.IsOptimized),
                        AverageTravelTime = g.Average(r => r.TotalTravelTime?.TotalHours ?? 0),
                        TotalSavings = g.Sum(r => r.EstimatedTimeSavings?.TotalHours ?? 0)
                    }).ToList(),
                
                // Technician route performance
                TechnicianRouteStats = routes.GroupBy(r => r.TechnicianId)
                    .Where(g => g.Key.HasValue)
                    .Select(g => new TechnicianRouteStatsDto
                    {
                        TechnicianId = g.Key!.Value,
                        TechnicianName = g.First().Technician?.Name ?? "Unknown",
                        TotalRoutes = g.Count(),
                        OptimizedRoutes = g.Count(r => r.IsOptimized),
                        AverageTravelTime = g.Average(r => r.TotalTravelTime?.TotalHours ?? 0),
                        TotalSavings = g.Sum(r => r.EstimatedTimeSavings?.TotalHours ?? 0)
                    }).ToList()
            };

            return Ok(new ApiResponse<RouteOptimizationReportDto>
            {
                Success = true,
                Data = report,
                Message = "Route optimization report generated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating route optimization report");
            return StatusCode(500, new ApiResponse<RouteOptimizationReportDto>
            {
                Success = false,
                Message = "An error occurred while generating route optimization report",
                Errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// Export report data to CSV format
    /// </summary>
    [HttpGet("export/{reportType}")]
    public async Task<ActionResult> ExportReport(
        string reportType,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? tenantId,
        [FromQuery] string format = "csv")
    {
        try
        {
            var dateStart = startDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var dateEnd = endDate ?? DateTime.UtcNow.Date;

            string csvContent;
            string fileName;

            switch (reportType.ToLowerInvariant())
            {
                case "jobs":
                    csvContent = await GenerateJobsCsv(dateStart, dateEnd, tenantId);
                    fileName = $"jobs_report_{dateStart:yyyy-MM-dd}_to_{dateEnd:yyyy-MM-dd}.csv";
                    break;
                    
                case "technicians":
                    csvContent = await GenerateTechniciansCsv(dateStart, dateEnd, tenantId);
                    fileName = $"technicians_report_{dateStart:yyyy-MM-dd}_to_{dateEnd:yyyy-MM-dd}.csv";
                    break;
                    
                case "routes":
                    csvContent = await GenerateRoutesCsv(dateStart, dateEnd, tenantId);
                    fileName = $"routes_report_{dateStart:yyyy-MM-dd}_to_{dateEnd:yyyy-MM-dd}.csv";
                    break;
                    
                default:
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid report type. Supported types: jobs, technicians, routes"
                    });
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting {ReportType} report", reportType);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "An error occurred while exporting the report",
                Errors = new[] { ex.Message }
            });
        }
    }

    #region Private Helper Methods

    private double CalculateAverageUtilization(List<Technician> technicians, DateTime startDate, DateTime endDate)
    {
        if (!technicians.Any()) return 0;

        var utilizationRates = technicians.Select(t => CalculateUtilizationRate(t, startDate, endDate));
        return utilizationRates.Average();
    }

    private double CalculateUtilizationRate(Technician technician, DateTime startDate, DateTime endDate)
    {
        var totalWorkingHours = CalculateTotalWorkingHours(technician, startDate, endDate);
        if (totalWorkingHours == 0) return 0;

        var scheduledHours = technician.ServiceJobs
            .Where(j => j.ScheduledDate >= startDate && j.ScheduledDate <= endDate)
            .Sum(j => j.EstimatedDuration?.TotalHours ?? 0);

        return Math.Min(100, (scheduledHours / totalWorkingHours) * 100);
    }

    private double CalculateTotalWorkingHours(Technician technician, DateTime startDate, DateTime endDate)
    {
        var totalHours = 0.0;
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            var dayOfWeek = currentDate.DayOfWeek;
            var workingHours = technician.WorkingHours
                .FirstOrDefault(wh => wh.DayOfWeek == dayOfWeek && wh.IsAvailable);

            if (workingHours != null)
            {
                totalHours += (workingHours.EndTime - workingHours.StartTime).TotalHours;
            }

            currentDate = currentDate.AddDays(1);
        }

        return totalHours;
    }

    private double CalculateMedian(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        if (!sorted.Any()) return 0;

        var count = sorted.Count;
        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
        }
        return sorted[count / 2];
    }

    private async Task<string> GenerateJobsCsv(DateTime startDate, DateTime endDate, Guid? tenantId)
    {
        var query = _context.ServiceJobs
            .Include(j => j.Technician)
            .Where(j => j.ScheduledDate >= startDate && j.ScheduledDate <= endDate);

        if (tenantId.HasValue)
            query = query.Where(j => j.TenantId == tenantId.Value);

        var jobs = await query.ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("JobNumber,Title,Status,Priority,JobType,ScheduledDate,EstimatedDuration,ActualDuration,TechnicianName,CustomerName,EstimatedCost,ActualCost,City,CreatedAt");

        foreach (var job in jobs)
        {
            csv.AppendLine($"{job.JobNumber}," +
                         $"\"{job.Title}\"," +
                         $"{job.Status}," +
                         $"{job.Priority}," +
                         $"{job.JobType}," +
                         $"{job.ScheduledDate:yyyy-MM-dd HH:mm}," +
                         $"{job.EstimatedDuration?.TotalHours ?? 0}," +
                         $"{job.ActualDuration?.TotalHours ?? 0}," +
                         $"\"{job.Technician?.Name ?? "Unassigned"}\"," +
                         $"\"{job.CustomerName}\"," +
                         $"{job.EstimatedCost ?? 0}," +
                         $"{job.ActualCost ?? 0}," +
                         $"\"{job.ServiceAddress.City}\"," +
                         $"{job.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        return csv.ToString();
    }

    private async Task<string> GenerateTechniciansCsv(DateTime startDate, DateTime endDate, Guid? tenantId)
    {
        var query = _context.Technicians
            .Include(t => t.ServiceJobs.Where(j => j.ScheduledDate >= startDate && j.ScheduledDate <= endDate));

        if (tenantId.HasValue)
            query = query.Where(t => t.TenantId == tenantId.Value);

        var technicians = await query.ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("EmployeeId,Name,Email,Status,BaseLocation,HourlyRate,TotalJobs,CompletedJobs,CompletionRate,TotalRevenue,UtilizationRate");

        foreach (var tech in technicians)
        {
            var completionRate = tech.ServiceJobs.Count > 0 ? 
                (double)tech.ServiceJobs.Count(j => j.Status == ServiceJobStatus.Completed) / tech.ServiceJobs.Count * 100 : 0;
            var totalRevenue = tech.ServiceJobs.Where(j => j.Status == ServiceJobStatus.Completed).Sum(j => j.ActualCost ?? 0);
            var utilizationRate = CalculateUtilizationRate(tech, startDate, endDate);

            csv.AppendLine($"{tech.EmployeeId}," +
                         $"\"{tech.Name}\"," +
                         $"{tech.Email}," +
                         $"{tech.Status}," +
                         $"\"{tech.BaseLocation ?? ""}\"," +
                         $"{tech.HourlyRate}," +
                         $"{tech.ServiceJobs.Count}," +
                         $"{tech.ServiceJobs.Count(j => j.Status == ServiceJobStatus.Completed)}," +
                         $"{completionRate:F2}," +
                         $"{totalRevenue}," +
                         $"{utilizationRate:F2}");
        }

        return csv.ToString();
    }

    private async Task<string> GenerateRoutesCsv(DateTime startDate, DateTime endDate, Guid? tenantId)
    {
        var query = _context.Routes
            .Include(r => r.Technician)
            .Where(r => r.Date >= startDate && r.Date <= endDate);

        if (tenantId.HasValue)
            query = query.Where(r => r.TenantId == tenantId.Value);

        var routes = await query.ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("RouteName,Date,TechnicianName,TotalDistance,TotalTravelTime,IsOptimized,OptimizationAlgorithm,EstimatedTimeSavings,EstimatedFuelSavings,StopCount");

        foreach (var route in routes)
        {
            csv.AppendLine($"\"{route.RouteName}\"," +
                         $"{route.Date:yyyy-MM-dd}," +
                         $"\"{route.Technician?.Name ?? "Unassigned"}\"," +
                         $"{route.TotalDistance ?? 0}," +
                         $"{route.TotalTravelTime?.TotalHours ?? 0}," +
                         $"{route.IsOptimized}," +
                         $"\"{route.OptimizationAlgorithm ?? ""}\"," +
                         $"{route.EstimatedTimeSavings?.TotalHours ?? 0}," +
                         $"{route.EstimatedFuelSavings ?? 0}," +
                         $"{route.RouteStops.Count}");
        }

        return csv.ToString();
    }

    #endregion
}

// Report DTOs
public class DashboardReportDto
{
    public string ReportPeriod { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int PendingJobs { get; set; }
    public int CancelledJobs { get; set; }
    public double CompletionRate { get; set; }
    public double AverageJobDuration { get; set; }
    public int ActiveTechnicians { get; set; }
    public int TotalTechnicians { get; set; }
    public double AverageUtilizationRate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageJobValue { get; set; }
    public List<RecentJobDto> RecentJobs { get; set; } = new();
}

public class RecentJobDto
{
    public Guid JobId { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TechnicianPerformanceDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int CancelledJobs { get; set; }
    public double CompletionRate { get; set; }
    public double AverageJobDuration { get; set; }
    public decimal TotalRevenue { get; set; }
    public double UtilizationRate { get; set; }
    public int SkillsCount { get; set; }
    public string BaseLocation { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
}

public class JobAnalyticsDto
{
    public string ReportPeriod { get; set; } = string.Empty;
    public int TotalJobs { get; set; }
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();
    public Dictionary<string, int> PriorityBreakdown { get; set; } = new();
    public Dictionary<string, int> JobTypeBreakdown { get; set; } = new();
    public double AverageDuration { get; set; }
    public double MedianDuration { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageJobValue { get; set; }
    public List<DailyTrendDto> DailyTrends { get; set; } = new();
    public Dictionary<string, int> LocationAnalysis { get; set; } = new();
}

public class DailyTrendDto
{
    public DateTime Date { get; set; }
    public int JobCount { get; set; }
    public int CompletedCount { get; set; }
    public decimal Revenue { get; set; }
}

public class RouteOptimizationReportDto
{
    public string ReportPeriod { get; set; } = string.Empty;
    public int TotalRoutes { get; set; }
    public int OptimizedRoutes { get; set; }
    public double AverageTravelTime { get; set; }
    public double AverageDistance { get; set; }
    public decimal TotalFuelSavings { get; set; }
    public double TotalTimeSavings { get; set; }
    public Dictionary<string, AlgorithmStatsDto> AlgorithmBreakdown { get; set; } = new();
    public List<OptimizationTrendDto> DailyOptimization { get; set; } = new();
    public List<TechnicianRouteStatsDto> TechnicianRouteStats { get; set; } = new();
}

public class AlgorithmStatsDto
{
    public int Count { get; set; }
    public double AverageTravelTime { get; set; }
    public double AverageDistance { get; set; }
    public double AverageSavings { get; set; }
}

public class OptimizationTrendDto
{
    public DateTime Date { get; set; }
    public int RoutesCreated { get; set; }
    public int RoutesOptimized { get; set; }
    public double AverageTravelTime { get; set; }
    public double TotalSavings { get; set; }
}

public class TechnicianRouteStatsDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public int TotalRoutes { get; set; }
    public int OptimizedRoutes { get; set; }
    public double AverageTravelTime { get; set; }
    public double TotalSavings { get; set; }
}
