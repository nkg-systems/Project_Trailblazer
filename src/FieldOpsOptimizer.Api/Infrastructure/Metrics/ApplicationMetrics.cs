using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FieldOpsOptimizer.Api.Infrastructure.Metrics;

public class ApplicationMetrics
{
    public const string MeterName = "FieldOpsOptimizer.Api";
    private readonly Meter _meter;

    // Counters
    private readonly Counter<long> _jobsCreatedCounter;
    private readonly Counter<long> _jobsCompletedCounter;
    private readonly Counter<long> _jobsAssignedCounter;
    private readonly Counter<long> _techniciansCreatedCounter;
    private readonly Counter<long> _routesOptimizedCounter;
    private readonly Counter<long> _apiRequestsCounter;
    private readonly Counter<long> _apiErrorsCounter;

    // Histograms
    private readonly Histogram<double> _requestDurationHistogram;
    private readonly Histogram<double> _jobDurationHistogram;
    private readonly Histogram<double> _routeOptimizationDurationHistogram;

    // Gauges (using ObservableGauge)
    private readonly ObservableGauge<int> _activeJobsGauge;
    private readonly ObservableGauge<int> _availableTechniciansGauge;
    private readonly ObservableGauge<int> _activeRoutesGauge;

    private volatile int _activeJobsCount;
    private volatile int _availableTechniciansCount;
    private volatile int _activeRoutesCount;

    public ApplicationMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName, "1.0.0");

        // Initialize counters
        _jobsCreatedCounter = _meter.CreateCounter<long>("jobs_created_total", "count", "Total number of jobs created");
        _jobsCompletedCounter = _meter.CreateCounter<long>("jobs_completed_total", "count", "Total number of jobs completed");
        _jobsAssignedCounter = _meter.CreateCounter<long>("jobs_assigned_total", "count", "Total number of jobs assigned to technicians");
        _techniciansCreatedCounter = _meter.CreateCounter<long>("technicians_created_total", "count", "Total number of technicians created");
        _routesOptimizedCounter = _meter.CreateCounter<long>("routes_optimized_total", "count", "Total number of routes optimized");
        _apiRequestsCounter = _meter.CreateCounter<long>("api_requests_total", "count", "Total number of API requests");
        _apiErrorsCounter = _meter.CreateCounter<long>("api_errors_total", "count", "Total number of API errors");

        // Initialize histograms
        _requestDurationHistogram = _meter.CreateHistogram<double>("request_duration_ms", "milliseconds", "Duration of HTTP requests");
        _jobDurationHistogram = _meter.CreateHistogram<double>("job_duration_hours", "hours", "Duration of completed jobs");
        _routeOptimizationDurationHistogram = _meter.CreateHistogram<double>("route_optimization_duration_ms", "milliseconds", "Duration of route optimization operations");

        // Initialize gauges
        _activeJobsGauge = _meter.CreateObservableGauge("active_jobs", () => _activeJobsCount, "count", "Number of active jobs");
        _availableTechniciansGauge = _meter.CreateObservableGauge("available_technicians", () => _availableTechniciansCount, "count", "Number of available technicians");
        _activeRoutesGauge = _meter.CreateObservableGauge("active_routes", () => _activeRoutesCount, "count", "Number of active routes");
    }

    // Job metrics
    public void RecordJobCreated(string tenantId, string priority = "medium")
    {
        _jobsCreatedCounter.Add(1, new TagList { { "tenant_id", tenantId }, { "priority", priority } });
    }

    public void RecordJobCompleted(string tenantId, double durationHours, string priority = "medium")
    {
        _jobsCompletedCounter.Add(1, new TagList { { "tenant_id", tenantId }, { "priority", priority } });
        _jobDurationHistogram.Record(durationHours, new TagList { { "tenant_id", tenantId }, { "priority", priority } });
    }

    public void RecordJobAssigned(string tenantId, string technicianId)
    {
        _jobsAssignedCounter.Add(1, new TagList { { "tenant_id", tenantId }, { "technician_id", technicianId } });
    }

    // Technician metrics
    public void RecordTechnicianCreated(string tenantId)
    {
        _techniciansCreatedCounter.Add(1, new TagList { { "tenant_id", tenantId } });
    }

    // Route metrics
    public void RecordRouteOptimized(string tenantId, double optimizationDurationMs, int stopCount)
    {
        _routesOptimizedCounter.Add(1, new TagList { { "tenant_id", tenantId }, { "stop_count", stopCount.ToString() } });
        _routeOptimizationDurationHistogram.Record(optimizationDurationMs, new TagList { { "tenant_id", tenantId } });
    }

    // API metrics
    public void RecordApiRequest(string method, string endpoint, string statusCode, double durationMs)
    {
        _apiRequestsCounter.Add(1, new TagList 
        { 
            { "method", method }, 
            { "endpoint", endpoint }, 
            { "status_code", statusCode } 
        });
        
        _requestDurationHistogram.Record(durationMs, new TagList 
        { 
            { "method", method }, 
            { "endpoint", endpoint }, 
            { "status_code", statusCode } 
        });
    }

    public void RecordApiError(string method, string endpoint, string errorType, string? exceptionType = null)
    {
        var tags = new TagList { { "method", method }, { "endpoint", endpoint }, { "error_type", errorType } };
        
        if (!string.IsNullOrEmpty(exceptionType))
        {
            tags.Add("exception_type", exceptionType);
        }

        _apiErrorsCounter.Add(1, tags);
    }

    // Gauge updates
    public void UpdateActiveJobsCount(int count)
    {
        _activeJobsCount = count;
    }

    public void UpdateAvailableTechniciansCount(int count)
    {
        _availableTechniciansCount = count;
    }

    public void UpdateActiveRoutesCount(int count)
    {
        _activeRoutesCount = count;
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}

public class MetricsService
{
    private readonly ApplicationMetrics _metrics;
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(ApplicationMetrics metrics, ILogger<MetricsService> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<BusinessMetrics> GetBusinessMetricsAsync(string tenantId, DateTime fromDate, DateTime toDate)
    {
        try
        {
            _logger.LogInformation("Retrieving business metrics for tenant {TenantId} from {FromDate} to {ToDate}", 
                tenantId, fromDate, toDate);

            // This would typically query your database for actual metrics
            // For now, we'll return sample data structure
            return new BusinessMetrics
            {
                TenantId = tenantId,
                PeriodStart = fromDate,
                PeriodEnd = toDate,
                TotalJobs = await GetTotalJobsAsync(tenantId, fromDate, toDate),
                CompletedJobs = await GetCompletedJobsAsync(tenantId, fromDate, toDate),
                TotalRevenue = await GetTotalRevenueAsync(tenantId, fromDate, toDate),
                AverageJobDuration = await GetAverageJobDurationAsync(tenantId, fromDate, toDate),
                TechnicianUtilization = await GetTechnicianUtilizationAsync(tenantId, fromDate, toDate),
                RoutesOptimized = await GetRoutesOptimizedAsync(tenantId, fromDate, toDate)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business metrics for tenant {TenantId}", tenantId);
            throw;
        }
    }

    private Task<int> GetTotalJobsAsync(string tenantId, DateTime fromDate, DateTime toDate)
    {
        // TODO: Implement actual database query
        return Task.FromResult(0);
    }

    private Task<int> GetCompletedJobsAsync(string tenantId, DateTime fromDate, DateTime toDate)
    {
        // TODO: Implement actual database query
        return Task.FromResult(0);
    }

    private Task<decimal> GetTotalRevenueAsync(string tenantId, DateTime fromDate, DateTime toDate)
    {
        // TODO: Implement actual database query
        return Task.FromResult(0m);
    }

    private Task<TimeSpan> GetAverageJobDurationAsync(string tenantId, DateTime fromDate, DateTime toDate)
    {
        // TODO: Implement actual database query
        return Task.FromResult(TimeSpan.Zero);
    }

    private Task<double> GetTechnicianUtilizationAsync(string tenantId, DateTime fromDate, DateTime toDate)
    {
        // TODO: Implement actual database query
        return Task.FromResult(0.0);
    }

    private Task<int> GetRoutesOptimizedAsync(string tenantId, DateTime fromDate, DateTime toDate)
    {
        // TODO: Implement actual database query
        return Task.FromResult(0);
    }
}

public class BusinessMetrics
{
    public string TenantId { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public decimal TotalRevenue { get; set; }
    public TimeSpan AverageJobDuration { get; set; }
    public double TechnicianUtilization { get; set; }
    public int RoutesOptimized { get; set; }
    public double CompletionRate => TotalJobs > 0 ? (double)CompletedJobs / TotalJobs : 0.0;
}
