using FieldOpsOptimizer.Api.Infrastructure.Metrics;
using FieldOpsOptimizer.Api.Infrastructure.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FieldOpsOptimizer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly MetricsService _metricsService;
    private readonly ApplicationMetrics _applicationMetrics;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        MetricsService metricsService, 
        ApplicationMetrics applicationMetrics,
        ILogger<MetricsController> logger)
    {
        _metricsService = metricsService;
        _applicationMetrics = applicationMetrics;
        _logger = logger;
    }

    /// <summary>
    /// Get business metrics for a specific time period
    /// </summary>
    [HttpGet("business")]
    public async Task<ActionResult<BusinessMetrics>> GetBusinessMetrics(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var tenantId = GetTenantId();
            var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
            var to = toDate ?? DateTime.UtcNow;

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["TenantId"] = tenantId,
                ["FromDate"] = from,
                ["ToDate"] = to
            }))
            {
                _logger.LogInformation("Retrieving business metrics for period {FromDate} to {ToDate}", from, to);
                
                var metrics = await _metricsService.GetBusinessMetricsAsync(tenantId, from, to);
                
                _logger.LogBusinessEvent("metrics", "business_metrics_retrieved", new Dictionary<string, object>
                {
                    ["period_days"] = (to - from).TotalDays,
                    ["total_jobs"] = metrics.TotalJobs,
                    ["completed_jobs"] = metrics.CompletedJobs
                });

                return Ok(metrics);
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithContext(ex, "GetBusinessMetrics", new Dictionary<string, object>
            {
                ["FromDate"] = fromDate?.ToString() ?? "null",
                ["ToDate"] = toDate?.ToString() ?? "null"
            });
            
            return StatusCode(500, "An error occurred while retrieving business metrics");
        }
    }

    /// <summary>
    /// Get system health and performance metrics
    /// </summary>
    [HttpGet("system")]
    [AllowAnonymous] // Allow monitoring systems to access this without auth
    public ActionResult<SystemMetrics> GetSystemMetrics()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var startTime = DateTime.UtcNow.AddMilliseconds(-Environment.TickCount64);

            var metrics = new SystemMetrics
            {
                Timestamp = DateTime.UtcNow,
                Uptime = DateTime.UtcNow - startTime,
                MemoryUsage = new MemoryMetrics
                {
                    WorkingSetMB = process.WorkingSet64 / 1024 / 1024,
                    PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
                    GCTotalMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024
                },
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                CpuTime = process.TotalProcessorTime
            };

            _logger.LogDebug("System metrics retrieved: Memory={MemoryMB}MB, Threads={ThreadCount}, Uptime={Uptime}",
                metrics.MemoryUsage.WorkingSetMB, metrics.ThreadCount, metrics.Uptime);

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system metrics");
            return StatusCode(500, "An error occurred while retrieving system metrics");
        }
    }

    /// <summary>
    /// Get API performance metrics
    /// </summary>
    [HttpGet("performance")]
    public ActionResult<ApiPerformanceMetrics> GetPerformanceMetrics(
        [FromQuery] int? hours = 24)
    {
        try
        {
            var hoursBack = Math.Min(hours ?? 24, 168); // Max 1 week
            var fromTime = DateTime.UtcNow.AddHours(-hoursBack);

            _logger.LogInformation("Retrieving API performance metrics for last {Hours} hours", hoursBack);

            // In a real implementation, these would be calculated from your metrics store
            var metrics = new ApiPerformanceMetrics
            {
                PeriodStart = fromTime,
                PeriodEnd = DateTime.UtcNow,
                TotalRequests = 0, // TODO: Calculate from metrics
                AverageResponseTimeMs = 0, // TODO: Calculate from metrics
                ErrorRate = 0.0, // TODO: Calculate from metrics
                RequestsPerMinute = 0.0, // TODO: Calculate from metrics
                EndpointMetrics = new List<EndpointMetric>() // TODO: Populate with actual data
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics");
            return StatusCode(500, "An error occurred while retrieving performance metrics");
        }
    }

    /// <summary>
    /// Reset metrics counters (admin only)
    /// </summary>
    [HttpPost("reset")]
    [Authorize(Roles = "Admin")]
    public ActionResult ResetMetrics()
    {
        try
        {
            var userId = GetUserId();
            _logger.LogSecurityEvent("metrics_reset", userId, "Metrics counters reset");
            
            // TODO: Implement metrics reset logic
            _logger.LogWarning("Metrics reset requested by user {UserId}", userId);
            
            return Ok(new { message = "Metrics reset successfully", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting metrics");
            return StatusCode(500, "An error occurred while resetting metrics");
        }
    }

    private string GetTenantId()
    {
        return User.FindFirst("tenant")?.Value ?? "unknown";
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value ?? "unknown";
    }
}

public class SystemMetrics
{
    public DateTime Timestamp { get; set; }
    public TimeSpan Uptime { get; set; }
    public MemoryMetrics MemoryUsage { get; set; } = new();
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public TimeSpan CpuTime { get; set; }
}

public class MemoryMetrics
{
    public long WorkingSetMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public long GCTotalMemoryMB { get; set; }
}

public class ApiPerformanceMetrics
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public long TotalRequests { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double ErrorRate { get; set; }
    public double RequestsPerMinute { get; set; }
    public List<EndpointMetric> EndpointMetrics { get; set; } = new();
}

public class EndpointMetric
{
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public double AverageResponseTimeMs { get; set; }
    public double ErrorRate { get; set; }
    public double P95ResponseTimeMs { get; set; }
    public double P99ResponseTimeMs { get; set; }
}
