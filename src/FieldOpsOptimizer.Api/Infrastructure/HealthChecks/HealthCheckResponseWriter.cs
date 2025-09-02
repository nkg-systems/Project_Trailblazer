using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace FieldOpsOptimizer.Api.Infrastructure.HealthChecks;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task WriteResponse(HttpContext context, HealthReport healthReport)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new HealthCheckResponse
        {
            Status = healthReport.Status.ToString(),
            Duration = healthReport.TotalDuration,
            Info = healthReport.Entries.Select(x => new HealthCheckInfo
            {
                Key = x.Key,
                Description = x.Value.Description,
                Duration = x.Value.Duration,
                Status = x.Value.Status.ToString(),
                Error = x.Value.Exception?.Message,
                Data = x.Value.Data
            }).ToArray()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, Options));
    }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public HealthCheckInfo[] Info { get; set; } = Array.Empty<HealthCheckInfo>();
}

public class HealthCheckInfo
{
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public IReadOnlyDictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
}
