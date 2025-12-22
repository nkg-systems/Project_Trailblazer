using FieldOpsOptimizer.Api.Infrastructure.Metrics;
using FieldOpsOptimizer.Api.Infrastructure.Tracing;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FieldOpsOptimizer.Api.Infrastructure.Logging;

public static class LoggingExtensions
{
    // Job-specific logging
    public static void LogJobCreated(this ILogger logger, string jobId, string customerName, string tenantId, 
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartJobActivity("created", jobId, tenantId);
        activity?.AddBusinessContext("job_creation", new Dictionary<string, object>
        {
            ["customer_name"] = customerName
        });

        logger.LogInformation("Job {JobId} created for customer {CustomerName} in tenant {TenantId} by {CallerName}",
            jobId, customerName, tenantId, callerName);
    }

    public static void LogJobAssigned(this ILogger logger, string jobId, string technicianId, string tenantId,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartJobActivity("assigned", jobId, tenantId);
        activity?.AddBusinessContext("job_assignment", new Dictionary<string, object>
        {
            ["technician_id"] = technicianId
        });

        logger.LogInformation("Job {JobId} assigned to technician {TechnicianId} in tenant {TenantId} by {CallerName}",
            jobId, technicianId, tenantId, callerName);
    }

    public static void LogJobCompleted(this ILogger logger, string jobId, string technicianId, TimeSpan duration, string tenantId,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartJobActivity("completed", jobId, tenantId);
        activity?.AddBusinessContext("job_completion", new Dictionary<string, object>
        {
            ["technician_id"] = technicianId,
            ["duration_hours"] = duration.TotalHours
        });

        logger.LogInformation("Job {JobId} completed by technician {TechnicianId} in {Duration} for tenant {TenantId} by {CallerName}",
            jobId, technicianId, duration, tenantId, callerName);
    }

    public static void LogJobStatusChanged(this ILogger logger, string jobId, string oldStatus, string newStatus, string tenantId,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartJobActivity("status_changed", jobId, tenantId);
        activity?.AddBusinessContext("status_change", new Dictionary<string, object>
        {
            ["old_status"] = oldStatus,
            ["new_status"] = newStatus
        });

        logger.LogInformation("Job {JobId} status changed from {OldStatus} to {NewStatus} in tenant {TenantId} by {CallerName}",
            jobId, oldStatus, newStatus, tenantId, callerName);
    }

    // Technician-specific logging
    public static void LogTechnicianCreated(this ILogger logger, string technicianId, string email, string tenantId,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartTechnicianActivity("created", technicianId, tenantId);
        activity?.AddBusinessContext("technician_creation", new Dictionary<string, object>
        {
            ["email"] = email
        });

        logger.LogInformation("Technician {TechnicianId} created with email {Email} in tenant {TenantId} by {CallerName}",
            technicianId, email, tenantId, callerName);
    }

    public static void LogTechnicianStatusChanged(this ILogger logger, string technicianId, string oldStatus, string newStatus, string tenantId,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartTechnicianActivity("status_changed", technicianId, tenantId);
        activity?.AddBusinessContext("status_change", new Dictionary<string, object>
        {
            ["old_status"] = oldStatus,
            ["new_status"] = newStatus
        });

        logger.LogInformation("Technician {TechnicianId} status changed from {OldStatus} to {NewStatus} in tenant {TenantId} by {CallerName}",
            technicianId, oldStatus, newStatus, tenantId, callerName);
    }

    public static void LogSkillAdded(this ILogger logger, string technicianId, string skillName, int proficiency, string tenantId,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartTechnicianActivity("skill_added", technicianId, tenantId);
        activity?.AddBusinessContext("skill_management", new Dictionary<string, object>
        {
            ["skill_name"] = skillName,
            ["proficiency"] = proficiency
        });

        logger.LogInformation("Skill {SkillName} (level {Proficiency}) added to technician {TechnicianId} in tenant {TenantId} by {CallerName}",
            skillName, proficiency, technicianId, tenantId, callerName);
    }

    // Route-specific logging
    public static void LogRouteCreated(this ILogger logger, string routeId, string technicianId, DateTime date, string tenantId,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartRouteActivity("created", routeId, tenantId);
        activity?.AddBusinessContext("route_creation", new Dictionary<string, object>
        {
            ["technician_id"] = technicianId,
            ["date"] = date.ToString("yyyy-MM-dd")
        });

        logger.LogInformation("Route {RouteId} created for technician {TechnicianId} on {Date} in tenant {TenantId} by {CallerName}",
            routeId, technicianId, date, tenantId, callerName);
    }

    public static void LogRouteOptimized(this ILogger logger, string routeId, int stopCount, double optimizationTimeMs, string tenantId,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartRouteActivity("optimized", routeId, tenantId);
        activity?.AddBusinessContext("route_optimization", new Dictionary<string, object>
        {
            ["stop_count"] = stopCount,
            ["optimization_duration_ms"] = optimizationTimeMs
        });

        logger.LogInformation("Route {RouteId} optimized with {StopCount} stops in {OptimizationTime}ms for tenant {TenantId} by {CallerName}",
            routeId, stopCount, optimizationTimeMs, tenantId, callerName);
    }

    public static void LogRouteStarted(this ILogger logger, string routeId, string technicianId, string tenantId,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartRouteActivity("started", routeId, tenantId);
        activity?.AddBusinessContext("route_execution", new Dictionary<string, object>
        {
            ["technician_id"] = technicianId
        });

        logger.LogInformation("Route {RouteId} started by technician {TechnicianId} in tenant {TenantId} by {CallerName}",
            routeId, technicianId, tenantId, callerName);
    }

    // Database operation logging
    public static void LogDatabaseOperation(this ILogger logger, string operation, string tableName, int recordCount, TimeSpan duration,
        [CallerMemberName] string? callerName = null)
    {
        using var activity = ActivitySourceProvider.Source.StartDatabaseActivity(operation, tableName);
        activity?.AddBusinessContext("database_operation", new Dictionary<string, object>
        {
            ["record_count"] = recordCount,
            ["duration_ms"] = duration.TotalMilliseconds
        });

        logger.LogDebug("Database {Operation} on {TableName} affected {RecordCount} records in {Duration}ms by {CallerName}",
            operation, tableName, recordCount, duration, callerName);
    }

    // Performance logging
    public static void LogPerformanceMetric(this ILogger logger, string operationType, string operationName, TimeSpan duration, 
        Dictionary<string, object>? additionalData = null, [CallerMemberName] string? callerName = null)
    {
        var logData = new Dictionary<string, object>
        {
            ["OperationType"] = operationType,
            ["OperationName"] = operationName,
            ["DurationMs"] = duration.TotalMilliseconds,
            ["CallerName"] = callerName ?? "Unknown"
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                logData[kvp.Key] = kvp.Value;
            }
        }

        var logLevel = duration.TotalSeconds > 5 ? LogLevel.Warning : LogLevel.Information;
        
        logger.Log(logLevel, "Performance metric: {OperationType}.{OperationName} completed in {DurationMs}ms",
            operationType, operationName, duration.TotalMilliseconds);
    }

    // Business event logging
    public static void LogBusinessEvent(this ILogger logger, string eventType, string eventName, 
        Dictionary<string, object>? eventData = null, [CallerMemberName] string? callerName = null)
    {
        using var activity = Activity.Current?.Source.StartActivity($"business.{eventType}");
        activity?.AddBusinessContext(eventName, eventData);

        var logMessage = $"Business event: {eventType}.{eventName}";
        
        if (eventData != null && eventData.Any())
        {
            logger.LogInformation("{LogMessage} with data {@EventData} by {CallerName}", 
                logMessage, eventData, callerName);
        }
        else
        {
            logger.LogInformation("{LogMessage} by {CallerName}", logMessage, callerName);
        }
    }

    // Security logging
    public static void LogSecurityEvent(this ILogger logger, string eventType, string userId, string? details = null,
        [CallerMemberName] string? callerName = null)
    {
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["SecurityEvent"] = eventType,
            ["UserId"] = userId,
            ["CallerName"] = callerName ?? "Unknown"
        }))
        {
            logger.LogWarning("Security event: {EventType} for user {UserId}. Details: {Details}",
                eventType, userId, details ?? "None");
        }
    }

    // Error context logging
    public static void LogErrorWithContext(this ILogger logger, Exception exception, string operation, 
        Dictionary<string, object>? context = null, [CallerMemberName] string? callerName = null)
    {
        using var activity = Activity.Current;
        activity?.RecordException(exception);

        var logContext = new Dictionary<string, object>
        {
            ["Operation"] = operation,
            ["ExceptionType"] = exception.GetType().Name,
            ["CallerName"] = callerName ?? "Unknown"
        };

        if (context != null)
        {
            foreach (var kvp in context)
            {
                logContext[kvp.Key] = kvp.Value;
            }
        }

        using (logger.BeginScope(logContext))
        {
            logger.LogError(exception, "Error in operation {Operation}: {ErrorMessage}", 
                operation, exception.Message);
        }
    }
}

public static class StructuredLogging
{
    public static IDisposable? BeginJobScope(this ILogger logger, string jobId, string? tenantId = null)
    {
        var scope = new Dictionary<string, object>
        {
            ["JobId"] = jobId,
            ["ContextType"] = "Job"
        };

        if (!string.IsNullOrEmpty(tenantId))
            scope["TenantId"] = tenantId;

        return logger.BeginScope(scope);
    }

    public static IDisposable? BeginTechnicianScope(this ILogger logger, string technicianId, string? tenantId = null)
    {
        var scope = new Dictionary<string, object>
        {
            ["TechnicianId"] = technicianId,
            ["ContextType"] = "Technician"
        };

        if (!string.IsNullOrEmpty(tenantId))
            scope["TenantId"] = tenantId;

        return logger.BeginScope(scope);
    }

    public static IDisposable? BeginRouteScope(this ILogger logger, string routeId, string? tenantId = null)
    {
        var scope = new Dictionary<string, object>
        {
            ["RouteId"] = routeId,
            ["ContextType"] = "Route"
        };

        if (!string.IsNullOrEmpty(tenantId))
            scope["TenantId"] = tenantId;

        return logger.BeginScope(scope);
    }

    public static IDisposable? BeginOperationScope(this ILogger logger, string operationType, string operationId, 
        Dictionary<string, object>? additionalData = null)
    {
        var scope = new Dictionary<string, object>
        {
            ["OperationType"] = operationType,
            ["OperationId"] = operationId
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                scope[kvp.Key] = kvp.Value;
            }
        }

        return logger.BeginScope(scope);
    }
}

public class PerformanceLogger
{
    private readonly ILogger<PerformanceLogger> _logger;
    private readonly ApplicationMetrics _metrics;

    public PerformanceLogger(ILogger<PerformanceLogger> logger, ApplicationMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<T> MeasureAsync<T>(string operationType, string operationName, Func<Task<T>> operation, 
        Dictionary<string, object>? context = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        using var activity = ActivitySourceProvider.Source.StartActivity($"performance.{operationType}");
        activity?.SetTag("operation.name", operationName);
        activity?.SetTag("operation.id", operationId);

        using (_logger.BeginOperationScope(operationType, operationId, context))
        {
            try
            {
                _logger.LogDebug("Starting {OperationType} operation: {OperationName}", operationType, operationName);
                
                var result = await operation();
                
                stopwatch.Stop();
                _logger.LogPerformanceMetric(operationType, operationName, stopwatch.Elapsed, context);
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.RecordException(ex);
                
                _logger.LogErrorWithContext(ex, $"{operationType}.{operationName}", context);
                throw;
            }
        }
    }

    public async Task MeasureAsync(string operationType, string operationName, Func<Task> operation, 
        Dictionary<string, object>? context = null)
    {
        await MeasureAsync(operationType, operationName, async () =>
        {
            await operation();
            return Task.CompletedTask;
        }, context);
    }

    public T Measure<T>(string operationType, string operationName, Func<T> operation, 
        Dictionary<string, object>? context = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        using var activity = ActivitySourceProvider.Source.StartActivity($"performance.{operationType}");
        activity?.SetTag("operation.name", operationName);
        activity?.SetTag("operation.id", operationId);

        using (_logger.BeginOperationScope(operationType, operationId, context))
        {
            try
            {
                _logger.LogDebug("Starting {OperationType} operation: {OperationName}", operationType, operationName);
                
                var result = operation();
                
                stopwatch.Stop();
                _logger.LogPerformanceMetric(operationType, operationName, stopwatch.Elapsed, context);
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.RecordException(ex);
                
                _logger.LogErrorWithContext(ex, $"{operationType}.{operationName}", context);
                throw;
            }
        }
    }
}

public static class LoggerFactoryExtensions
{
    private static readonly ConcurrentDictionary<string, ILogger> LoggerCache = new();

    public static ILogger<T> CreateLogger<T>(this ILoggerFactory factory)
    {
        return factory.CreateLogger<T>();
    }

    public static ILogger CreateLogger(this ILoggerFactory factory, string categoryName)
    {
        return LoggerCache.GetOrAdd(categoryName, name => factory.CreateLogger(name));
    }

    public static ILogger GetLogger(this IServiceProvider serviceProvider, Type type)
    {
        var factory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return factory.CreateLogger(type.FullName ?? type.Name);
    }
}

// Performance timing helper
public readonly struct PerformanceTimer : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    private readonly Dictionary<string, object>? _context;

    public PerformanceTimer(ILogger logger, string operationName, Dictionary<string, object>? context = null)
    {
        _logger = logger;
        _operationName = operationName;
        _context = context;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _logger.LogPerformanceMetric("timer", _operationName, _stopwatch.Elapsed, _context);
    }
}

public static class TimingExtensions
{
    public static PerformanceTimer StartTimer(this ILogger logger, string operationName, 
        Dictionary<string, object>? context = null)
    {
        return new PerformanceTimer(logger, operationName, context);
    }
}
