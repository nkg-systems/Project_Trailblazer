using FieldOpsOptimizer.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace FieldOpsOptimizer.Api.Infrastructure.HealthChecks;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var healthChecksBuilder = services.AddHealthChecks();

        // Database health check
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            healthChecksBuilder.AddDbContextCheck<ApplicationDbContext>("database");
            
            // SQL Server specific health check
            if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            {
                healthChecksBuilder.AddSqlServer(connectionString, name: "sqlserver");
            }
        }

        // System health checks
        healthChecksBuilder
            .AddCheck<MemoryHealthCheck>("memory")
            .AddCheck<DiskSpaceHealthCheck>("diskspace")
            .AddCheck<ApplicationHealthCheck>("application");

        // External service health checks (add as needed)
        // healthChecksBuilder.AddUrlGroup(new Uri("https://api.external-service.com/health"), "external-api");

        return services;
    }

    public static IApplicationBuilder UseApplicationHealthChecks(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteResponse,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });

        app.UseHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = HealthCheckResponseWriter.WriteResponse
        });

        app.UseHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = HealthCheckResponseWriter.WriteResponse
        });

        return app;
    }
}

public class MemoryHealthCheck : IHealthCheck
{
    private readonly ILogger<MemoryHealthCheck> _logger;
    private const long MemoryThresholdBytes = 1_000_000_000; // 1GB

    public MemoryHealthCheck(ILogger<MemoryHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryUsed = process.WorkingSet64;
            var memoryUsedMB = memoryUsed / (1024 * 1024);

            var data = new Dictionary<string, object>
            {
                ["MemoryUsedBytes"] = memoryUsed,
                ["MemoryUsedMB"] = memoryUsedMB,
                ["ThresholdBytes"] = MemoryThresholdBytes
            };

            if (memoryUsed > MemoryThresholdBytes)
            {
                _logger.LogWarning("High memory usage detected: {MemoryUsedMB} MB", memoryUsedMB);
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Memory usage is high: {memoryUsedMB} MB", null, data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Memory usage is normal: {memoryUsedMB} MB", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking memory health");
            return Task.FromResult(HealthCheckResult.Unhealthy("Unable to check memory usage", ex));
        }
    }
}

public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private const long DiskSpaceThresholdBytes = 1_000_000_000; // 1GB

    public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();

            var data = new Dictionary<string, object>();
            var hasLowDiskSpace = false;

            foreach (var drive in drives)
            {
                var freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                var totalSpaceGB = drive.TotalSize / (1024 * 1024 * 1024);
                
                data[$"{drive.Name}FreeSpaceGB"] = freeSpaceGB;
                data[$"{drive.Name}TotalSpaceGB"] = totalSpaceGB;

                if (drive.AvailableFreeSpace < DiskSpaceThresholdBytes)
                {
                    hasLowDiskSpace = true;
                    _logger.LogWarning("Low disk space on drive {DriveName}: {FreeSpaceGB} GB available", 
                        drive.Name, freeSpaceGB);
                }
            }

            if (hasLowDiskSpace)
            {
                return Task.FromResult(HealthCheckResult.Degraded("Low disk space detected", null, data));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Disk space is adequate", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking disk space health");
            return Task.FromResult(HealthCheckResult.Unhealthy("Unable to check disk space", ex));
        }
    }
}

public class ApplicationHealthCheck : IHealthCheck
{
    private readonly ILogger<ApplicationHealthCheck> _logger;
    private static readonly DateTime StartupTime = DateTime.UtcNow;

    public ApplicationHealthCheck(ILogger<ApplicationHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var uptime = DateTime.UtcNow - StartupTime;
            var data = new Dictionary<string, object>
            {
                ["StartupTime"] = StartupTime,
                ["Uptime"] = uptime.ToString(),
                ["UptimeSeconds"] = uptime.TotalSeconds
            };

            _logger.LogDebug("Application health check completed. Uptime: {Uptime}", uptime);
            return Task.FromResult(HealthCheckResult.Healthy("Application is running", data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking application health");
            return Task.FromResult(HealthCheckResult.Unhealthy("Application health check failed", ex));
        }
    }
}
