using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Formatting.Compact;

namespace FieldOpsOptimizer.Api.Infrastructure.Logging;

public static class LoggingConfiguration
{
    public static void ConfigureLogging(IHostBuilder hostBuilder, IConfiguration configuration)
    {
        hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            var loggingConfig = configuration.GetSection("Logging");
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithThreadId()
                .Enrich.WithCorrelationId()
                .Enrich.WithProperty("Application", "FieldOpsOptimizer")
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Hosting"))
                .Filter.ByExcluding(Matching.FromSource("Microsoft.AspNetCore.Mvc.Infrastructure"))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Information)
                    .WriteTo.Console(new CompactJsonFormatter()))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Warning)
                    .WriteTo.File(
                        new CompactJsonFormatter(),
                        path: "logs/fieldops-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        shared: true))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                    .WriteTo.File(
                        new CompactJsonFormatter(),
                        path: "logs/errors/fieldops-errors-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 90,
                        shared: true));

            // Add database sink in production environments
            if (!string.IsNullOrEmpty(connectionString) && 
                (context.HostingEnvironment.IsProduction() || context.HostingEnvironment.IsStaging()))
            {
                loggerConfiguration.WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Warning)
                    .WriteTo.MSSqlServer(
                        connectionString: connectionString,
                        sinkOptions: new Serilog.Sinks.MSSqlServer.MSSqlServerSinkOptions
                        {
                            TableName = "Logs",
                            SchemaName = "dbo",
                            AutoCreateSqlTable = true,
                            BatchPostingLimit = 50
                        },
                        restrictedToMinimumLevel: LogEventLevel.Warning));
            }

            // Development specific logging
            if (context.HostingEnvironment.IsDevelopment())
            {
                loggerConfiguration
                    .WriteTo.Debug()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information);
            }
            else
            {
                loggerConfiguration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning);
            }
        });
    }
}

public static class LogEnricher
{
    public static LoggerConfiguration WithRequestId(this LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration.Enrich.With<RequestIdEnricher>();
    }

    public static LoggerConfiguration WithUserContext(this LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration.Enrich.With<UserContextEnricher>();
    }
}

public class RequestIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = GetHttpContext();
        if (httpContext?.Request != null)
        {
            var requestId = httpContext.TraceIdentifier;
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestId", requestId));
        }
    }

    private static Microsoft.AspNetCore.Http.HttpContext? GetHttpContext()
    {
        var httpContextAccessor = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        return httpContextAccessor.HttpContext;
    }
}

public class UserContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = GetHttpContext();
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst("sub")?.Value ?? 
                        httpContext.User.FindFirst("id")?.Value ?? 
                        "Unknown";
            var tenantId = httpContext.User.FindFirst("tenant")?.Value ?? "Unknown";
            
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", userId));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", tenantId));
        }
    }

    private static Microsoft.AspNetCore.Http.HttpContext? GetHttpContext()
    {
        var httpContextAccessor = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        return httpContextAccessor.HttpContext;
    }
}
