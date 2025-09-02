using FieldOpsOptimizer.Api.Infrastructure.Metrics;
using System.Diagnostics;
using System.Text;

namespace FieldOpsOptimizer.Api.Infrastructure.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly ApplicationMetrics _metrics;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, ApplicationMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;

        // Enable request body buffering for logging
        context.Request.EnableBuffering();
        
        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            // Log request details
            await LogRequestAsync(context, requestId);

            // Execute the request
            await _next(context);

            stopwatch.Stop();

            // Log response details
            await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);

            // Record metrics
            _metrics.RecordApiRequest(
                context.Request.Method,
                GetEndpointName(context),
                context.Response.StatusCode.ToString(),
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log error
            _logger.LogError(ex, "Request {RequestId} failed after {ElapsedMs}ms: {Method} {Path}",
                requestId, stopwatch.ElapsedMilliseconds, context.Request.Method, context.Request.Path);

            // Record error metrics
            _metrics.RecordApiError(
                context.Request.Method,
                GetEndpointName(context),
                "unhandled_exception",
                ex.GetType().Name);

            throw;
        }
        finally
        {
            // Copy response body back to original stream
            responseBodyStream.Position = 0;
            await responseBodyStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }

    private async Task LogRequestAsync(HttpContext context, string requestId)
    {
        try
        {
            var request = context.Request;
            var requestBody = await ReadRequestBodyAsync(request);

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
                ["UserId"] = context.User?.FindFirst("sub")?.Value ?? "Anonymous",
                ["TenantId"] = context.User?.FindFirst("tenant")?.Value ?? "Unknown"
            }))
            {
                _logger.LogInformation("Incoming request: {Method} {Path} {QueryString}",
                    request.Method, request.Path, request.QueryString);

                // Log request body for non-GET requests (excluding sensitive endpoints)
                if (!string.IsNullOrEmpty(requestBody) && ShouldLogRequestBody(request.Path))
                {
                    _logger.LogDebug("Request body: {RequestBody}", requestBody);
                }

                // Log request headers (excluding sensitive ones)
                LogHeaders(request.Headers, "Request");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log request details for {RequestId}", requestId);
        }
    }

    private async Task LogResponseAsync(HttpContext context, string requestId, long elapsedMs)
    {
        try
        {
            var response = context.Response;
            var responseBody = await ReadResponseBodyAsync(response);

            using (_logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId }))
            {
                var logLevel = GetLogLevelForStatusCode(response.StatusCode);
                
                _logger.Log(logLevel, "Outgoing response: {StatusCode} for {Method} {Path} in {ElapsedMs}ms",
                    response.StatusCode, context.Request.Method, context.Request.Path, elapsedMs);

                // Log response body for errors or when explicitly enabled
                if (ShouldLogResponseBody(response.StatusCode, context.Request.Path) && !string.IsNullOrEmpty(responseBody))
                {
                    _logger.LogDebug("Response body: {ResponseBody}", responseBody);
                }

                // Log performance warning for slow requests
                if (elapsedMs > 5000) // 5 seconds
                {
                    _logger.LogWarning("Slow request detected: {Method} {Path} took {ElapsedMs}ms",
                        context.Request.Method, context.Request.Path, elapsedMs);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log response details for {RequestId}", requestId);
        }
    }

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        try
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        try
        {
            response.Body.Position = 0;
            using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            response.Body.Position = 0;
            return body;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void LogHeaders(IHeaderDictionary headers, string type)
    {
        var sensitiveHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Cookie", "Set-Cookie", "X-API-Key", "X-Auth-Token"
        };

        var headerInfo = headers
            .Where(h => !sensitiveHeaders.Contains(h.Key))
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value.AsEnumerable()));

        if (headerInfo.Any())
        {
            _logger.LogDebug("{Type} headers: {@Headers}", type, headerInfo);
        }
    }

    private static bool ShouldLogRequestBody(string path)
    {
        var sensitiveEndpoints = new[]
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/users/password"
        };

        return !sensitiveEndpoints.Any(endpoint => 
            path.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldLogResponseBody(int statusCode, string path)
    {
        // Log response body for errors or debug endpoints
        return statusCode >= 400 || path.Contains("/debug", StringComparison.OrdinalIgnoreCase);
    }

    private static LogLevel GetLogLevelForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            >= 300 => LogLevel.Information,
            _ => LogLevel.Information
        };
    }

    private static string GetEndpointName(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.DisplayName != null)
        {
            return endpoint.DisplayName;
        }

        // Fallback to path template
        var path = context.Request.Path.ToString();
        return NormalizePath(path);
    }

    private static string NormalizePath(string path)
    {
        // Replace GUIDs and numeric IDs with placeholders for better grouping
        return System.Text.RegularExpressions.Regex.Replace(path, 
            @"/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|/\d+", 
            "/{id}");
    }
}
