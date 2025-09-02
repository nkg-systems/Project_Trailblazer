using FieldOpsOptimizer.Api.Infrastructure.HealthChecks;
using FieldOpsOptimizer.Api.Infrastructure.Logging;
using FieldOpsOptimizer.Api.Infrastructure.Metrics;
using FieldOpsOptimizer.Api.Infrastructure.Middleware;
using FieldOpsOptimizer.Api.Infrastructure.Tracing;
using Serilog;

namespace FieldOpsOptimizer.Api;

public static class ProgramExtensions
{
    public static WebApplicationBuilder ConfigureLoggingAndMonitoring(this WebApplicationBuilder builder)
    {
        // Configure Serilog logging
        LoggingConfiguration.ConfigureLogging(builder.Host, builder.Configuration);

        // Add monitoring services
        builder.Services.AddSingleton<ApplicationMetrics>();
        builder.Services.AddSingleton<MetricsService>();
        builder.Services.AddSingleton<PerformanceLogger>();

        // Add OpenTelemetry tracing and metrics
        builder.Services.AddOpenTelemetry(builder.Configuration);

        // Add health checks
        builder.Services.AddApplicationHealthChecks(builder.Configuration);

        // Add health checks UI
        builder.Services.AddHealthChecksUI(options =>
        {
            options.AddHealthCheckEndpoint("API Health", "/health");
            options.AddHealthCheckEndpoint("Ready Check", "/health/ready");
            options.AddHealthCheckEndpoint("Live Check", "/health/live");
            options.SetEvaluationTimeInSeconds(30);
            options.MaximumHistoryEntriesPerEndpoint(50);
        }).AddInMemoryStorage();

        // Add HTTP context accessor for enrichers
        builder.Services.AddHttpContextAccessor();

        return builder;
    }

    public static WebApplication ConfigureMonitoringPipeline(this WebApplication app)
    {
        // Add request logging middleware early in the pipeline
        if (app.Configuration.GetValue<bool>("Monitoring:EnableRequestLogging", true))
        {
            app.UseMiddleware<RequestLoggingMiddleware>();
        }

        // Add global exception handling
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // Use Serilog request logging
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (ex != null || httpContext.Response.StatusCode > 499)
                    return Serilog.Events.LogEventLevel.Error;

                if (httpContext.Response.StatusCode > 399)
                    return Serilog.Events.LogEventLevel.Warning;

                if (elapsed > 4000)
                    return Serilog.Events.LogEventLevel.Warning;

                return Serilog.Events.LogEventLevel.Information;
            };
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].FirstOrDefault());
                diagnosticContext.Set("ClientIP", GetClientIpAddress(httpContext));
                
                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
                    diagnosticContext.Set("TenantId", httpContext.User.FindFirst("tenant")?.Value);
                }
            };
        });

        // Add health check endpoints
        app.UseApplicationHealthChecks();

        // Add health checks UI
        app.UseHealthChecksUI(options =>
        {
            options.UIPath = "/health-ui";
            options.ApiPath = "/health-ui-api";
        });

        return app;
    }

    public static async Task<WebApplication> StartWithLoggingAsync(this WebApplication app)
    {
        try
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation("Starting FieldOpsOptimizer API...");
            logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
            logger.LogInformation("Content Root: {ContentRoot}", app.Environment.ContentRootPath);
            
            // Log configuration summary
            LogConfigurationSummary(logger, app.Configuration);

            await app.StartAsync();
            
            logger.LogInformation("FieldOpsOptimizer API started successfully");
            logger.LogInformation("Health checks available at: /health");
            logger.LogInformation("Health UI available at: /health-ui");
            
            if (app.Environment.IsDevelopment())
            {
                logger.LogInformation("Swagger UI available at: /swagger");
            }

            return app;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            throw;
        }
    }

    public static async Task StopWithLoggingAsync(this WebApplication app)
    {
        try
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Stopping FieldOpsOptimizer API...");
            
            await app.StopAsync();
            
            logger.LogInformation("FieldOpsOptimizer API stopped successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to stop gracefully");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void LogConfigurationSummary(ILogger logger, IConfiguration configuration)
    {
        try
        {
            // Log key configuration values (excluding sensitive data)
            logger.LogInformation("Configuration Summary:");
            logger.LogInformation("- Database Provider: {DatabaseProvider}", 
                GetDatabaseProvider(configuration.GetConnectionString("DefaultConnection")));
            
            logger.LogInformation("- Logging Level: {LogLevel}", 
                configuration["Serilog:MinimumLevel:Default"] ?? "Not configured");
            
            logger.LogInformation("- Health Checks Enabled: {HealthChecksEnabled}", 
                configuration.GetValue<bool>("HealthChecks:Enabled", true));
            
            logger.LogInformation("- Request Logging Enabled: {RequestLoggingEnabled}", 
                configuration.GetValue<bool>("Monitoring:EnableRequestLogging", true));
            
            logger.LogInformation("- Performance Metrics Enabled: {MetricsEnabled}", 
                configuration.GetValue<bool>("Monitoring:EnablePerformanceMetrics", true));
            
            logger.LogInformation("- Tracing Service: {ServiceName}", 
                configuration["Tracing:ServiceName"] ?? "Not configured");

            var otlpEndpoint = configuration["Tracing:OtlpEndpoint"];
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                logger.LogInformation("- OTLP Endpoint: {OtlpEndpoint}", otlpEndpoint);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log configuration summary");
        }
    }

    private static string GetDatabaseProvider(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "Not configured";

        if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            return "PostgreSQL";
        
        if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            return "SQL Server";
        
        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            return "SQLite";

        return "Unknown";
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP first (common in load balancers/proxies)
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            // Take the first IP if there are multiple
            return forwarded.Split(',')[0].Trim();
        }

        // Check for real IP (used by some proxies)
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
}

// Extension class for better organization
public static class MonitoringConfigurationExtensions
{
    public static IServiceCollection AddCustomMetrics(this IServiceCollection services, IConfiguration configuration)
    {
        // Add meters for .NET metrics
        services.AddMetrics();
        
        // Configure metrics export
        var metricsSection = configuration.GetSection("Metrics");
        if (metricsSection.Exists())
        {
            // Add metrics exporters based on configuration
            // This can be extended for Prometheus, OTLP, etc.
        }

        return services;
    }

    public static IApplicationBuilder UseCustomMetrics(this IApplicationBuilder app)
    {
        // Add metrics endpoint
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            // This can be extended to add metrics endpoints
            // endpoints.MapMetrics(); // For Prometheus metrics
        });

        return app;
    }
}

// Background service for periodic metric updates
public class MetricsUpdateService : BackgroundService
{
    private readonly ApplicationMetrics _metrics;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsUpdateService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(1);

    public MetricsUpdateService(
        ApplicationMetrics metrics, 
        IServiceProvider serviceProvider,
        ILogger<MetricsUpdateService> logger)
    {
        _metrics = metrics;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics update service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateMetricsAsync();
                await Task.Delay(_updateInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metrics");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait longer on error
            }
        }

        _logger.LogInformation("Metrics update service stopping");
    }

    private async Task UpdateMetricsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            // TODO: Update these with actual database queries
            // This is just a placeholder for the structure
            
            // Update active jobs count
            // var activeJobsCount = await GetActiveJobsCountAsync(scope);
            // _metrics.UpdateActiveJobsCount(activeJobsCount);

            // Update available technicians count
            // var availableTechniciansCount = await GetAvailableTechniciansCountAsync(scope);
            // _metrics.UpdateAvailableTechniciansCount(availableTechniciansCount);

            // Update active routes count
            // var activeRoutesCount = await GetActiveRoutesCountAsync(scope);
            // _metrics.UpdateActiveRoutesCount(activeRoutesCount);

            _logger.LogDebug("Metrics updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update metrics");
            throw;
        }
    }
}
