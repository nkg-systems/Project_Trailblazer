using FieldOpsOptimizer.Api.Infrastructure.Metrics;
using System.Net;
using System.Text.Json;

namespace FieldOpsOptimizer.Api.Infrastructure.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationMetrics _metrics;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public GlobalExceptionMiddleware(
        RequestDelegate next, 
        ILogger<GlobalExceptionMiddleware> logger, 
        IWebHostEnvironment environment,
        ApplicationMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var requestId = context.TraceIdentifier;
        var userId = context.User?.FindFirst("sub")?.Value ?? "Anonymous";
        var tenantId = context.User?.FindFirst("tenant")?.Value ?? "Unknown";

        // Log the exception with context
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["UserId"] = userId,
            ["TenantId"] = tenantId,
            ["RequestPath"] = context.Request.Path,
            ["RequestMethod"] = context.Request.Method
        }))
        {
            _logger.LogError(exception, "Unhandled exception occurred processing request {RequestId}", requestId);
        }

        // Record metrics
        _metrics.RecordApiError(
            context.Request.Method,
            context.Request.Path,
            exception.GetType().Name.ToLower(),
            exception.GetType().Name);

        // Determine response details based on exception type
        var (statusCode, errorType, userMessage) = GetErrorDetails(exception);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var errorResponse = new ErrorResponse
        {
            RequestId = requestId,
            Type = errorType,
            Title = GetErrorTitle(statusCode),
            Status = (int)statusCode,
            Detail = userMessage,
            Instance = context.Request.Path,
            Timestamp = DateTime.UtcNow
        };

        // Include stack trace and inner exception details in development
        if (_environment.IsDevelopment())
        {
            errorResponse.DeveloperMessage = exception.Message;
            errorResponse.StackTrace = exception.StackTrace;
            
            if (exception.InnerException != null)
            {
                errorResponse.InnerException = new InnerExceptionInfo
                {
                    Message = exception.InnerException.Message,
                    Type = exception.InnerException.GetType().Name,
                    StackTrace = exception.InnerException.StackTrace
                };
            }
        }

        var jsonResponse = JsonSerializer.Serialize(errorResponse, JsonOptions);
        await context.Response.WriteAsync(jsonResponse);
    }

    private static (HttpStatusCode statusCode, string errorType, string userMessage) GetErrorDetails(Exception exception)
    {
        return exception switch
        {
            ArgumentException or ArgumentNullException => 
                (HttpStatusCode.BadRequest, "validation_error", "Invalid request parameters."),
            
            UnauthorizedAccessException => 
                (HttpStatusCode.Unauthorized, "unauthorized", "You are not authorized to access this resource."),
            
            FileNotFoundException or DirectoryNotFoundException => 
                (HttpStatusCode.NotFound, "not_found", "The requested resource was not found."),
            
            InvalidOperationException => 
                (HttpStatusCode.Conflict, "invalid_operation", "The requested operation could not be completed."),
            
            TimeoutException => 
                (HttpStatusCode.RequestTimeout, "timeout", "The request timed out. Please try again."),
            
            NotSupportedException => 
                (HttpStatusCode.NotImplemented, "not_supported", "The requested operation is not supported."),
            
            _ => (HttpStatusCode.InternalServerError, "internal_error", "An unexpected error occurred. Please try again later.")
        };
    }

    private static string GetErrorTitle(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.Conflict => "Conflict",
            HttpStatusCode.RequestTimeout => "Request Timeout",
            HttpStatusCode.NotImplemented => "Not Implemented",
            HttpStatusCode.InternalServerError => "Internal Server Error",
            _ => "Error"
        };
    }
}

public class ErrorResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? DeveloperMessage { get; set; }
    public string? StackTrace { get; set; }
    public InnerExceptionInfo? InnerException { get; set; }
}

public class InnerExceptionInfo
{
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}
