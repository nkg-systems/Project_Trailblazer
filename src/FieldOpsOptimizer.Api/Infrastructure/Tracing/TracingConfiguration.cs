using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Diagnostics;

namespace FieldOpsOptimizer.Api.Infrastructure.Tracing;

public static class TracingConfiguration
{
    public const string ServiceName = "FieldOpsOptimizer.Api";
    public const string ServiceVersion = "1.0.0";

    public static IServiceCollection AddOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var tracingConfig = configuration.GetSection("Tracing");
        var otlpEndpoint = tracingConfig["OtlpEndpoint"];
        var jaegerEndpoint = tracingConfig["JaegerEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, ServiceVersion)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", 
                        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"),
                    new KeyValuePair<string, object>("service.instance.id", Environment.MachineName)
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ServiceName)
                    .AddSource("FieldOpsOptimizer.*")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.body.size", request.ContentLength ?? 0);
                            activity.SetTag("user.id", request.HttpContext.User?.FindFirst("sub")?.Value ?? "anonymous");
                            activity.SetTag("tenant.id", request.HttpContext.User?.FindFirst("tenant")?.Value ?? "unknown");
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.body.size", response.ContentLength ?? 0);
                        };
                        options.EnrichWithException = (activity, exception) =>
                        {
                            activity.SetTag("exception.escaped", true);
                            activity.SetTag("exception.type", exception.GetType().Name);
                        };
                    })
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.SetDbStatementForStoredProcedure = true;
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.operation.duration", activity.Duration.TotalMilliseconds);
                        };
                    })
                    .AddHttpClientInstrumentation();

                // Add exporters based on configuration
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }

                // Console exporter for development
                if (Environment.GetEnvironmentVariable("ASPNETCORE_Environment") == "Development")
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter("FieldOpsOptimizer.Api")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                // Add exporters
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }

                // Console exporter for development
                if (Environment.GetEnvironmentVariable("ASPNETCORE_Environment") == "Development")
                {
                    metrics.AddConsoleExporter();
                }
            });

        return services;
    }
}

public static class ActivitySourceProvider
{
    public static readonly ActivitySource Source = new(TracingConfiguration.ServiceName, TracingConfiguration.ServiceVersion);
}

public static class TracingExtensions
{
    public static Activity? StartJobActivity(this ActivitySource activitySource, string operationName, string jobId, string? tenantId = null)
    {
        var activity = activitySource.StartActivity($"job.{operationName}");
        activity?.SetTag("job.id", jobId);
        activity?.SetTag("operation.type", "job");
        
        if (!string.IsNullOrEmpty(tenantId))
            activity?.SetTag("tenant.id", tenantId);

        return activity;
    }

    public static Activity? StartTechnicianActivity(this ActivitySource activitySource, string operationName, string technicianId, string? tenantId = null)
    {
        var activity = activitySource.StartActivity($"technician.{operationName}");
        activity?.SetTag("technician.id", technicianId);
        activity?.SetTag("operation.type", "technician");
        
        if (!string.IsNullOrEmpty(tenantId))
            activity?.SetTag("tenant.id", tenantId);

        return activity;
    }

    public static Activity? StartRouteActivity(this ActivitySource activitySource, string operationName, string routeId, string? tenantId = null)
    {
        var activity = activitySource.StartActivity($"route.{operationName}");
        activity?.SetTag("route.id", routeId);
        activity?.SetTag("operation.type", "route");
        
        if (!string.IsNullOrEmpty(tenantId))
            activity?.SetTag("tenant.id", tenantId);

        return activity;
    }

    public static Activity? StartDatabaseActivity(this ActivitySource activitySource, string operationName, string? tableName = null)
    {
        var activity = activitySource.StartActivity($"database.{operationName}");
        activity?.SetTag("operation.type", "database");
        
        if (!string.IsNullOrEmpty(tableName))
            activity?.SetTag("db.sql.table", tableName);

        return activity;
    }

    public static void AddBusinessContext(this Activity? activity, string operationType, Dictionary<string, object>? additionalData = null)
    {
        if (activity == null) return;

        activity.SetTag("business.operation", operationType);
        
        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                activity.SetTag($"business.{kvp.Key}", kvp.Value.ToString());
            }
        }
    }

    public static void RecordException(this Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        
        if (exception.StackTrace != null)
        {
            activity.SetTag("exception.stacktrace", exception.StackTrace);
        }
    }
}
