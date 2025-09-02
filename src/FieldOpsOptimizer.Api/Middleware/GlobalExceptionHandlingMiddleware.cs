using FieldOpsOptimizer.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace FieldOpsOptimizer.Api.Middleware;

/// <summary>
/// Global exception handling middleware that catches unhandled exceptions
/// and converts them to appropriate HTTP responses
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An unhandled exception occurred during request processing");
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var problemDetails = exception switch
        {
            EntityNotFoundException ex => new ProblemDetails
            {
                Title = "Resource Not Found",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.NotFound,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Extensions = { ["entityName"] = ex.EntityName, ["entityId"] = ex.EntityId?.ToString() }
            },

            ValidationException ex => new ProblemDetails
            {
                Title = "Validation Error",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.BadRequest,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Extensions = { ["errors"] = ex.Errors }
            },

            BusinessRuleValidationException ex => new ProblemDetails
            {
                Title = "Business Rule Violation",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.BadRequest,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Extensions = { ["ruleName"] = ex.RuleName }
            },

            InvalidEntityStateException ex => new ProblemDetails
            {
                Title = "Invalid Entity State",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.Conflict,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                Extensions = { ["entityName"] = ex.EntityName, ["currentState"] = ex.CurrentState, ["requestedOperation"] = ex.RequestedOperation }
            },

            DuplicateEntityException ex => new ProblemDetails
            {
                Title = "Duplicate Entity",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.Conflict,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                Extensions = { ["entityName"] = ex.EntityName, ["duplicateProperty"] = ex.DuplicateProperty, ["duplicateValue"] = ex.DuplicateValue?.ToString() }
            },

            AccessDeniedException ex => new ProblemDetails
            {
                Title = "Access Denied",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.Forbidden,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                Extensions = { ["resource"] = ex.Resource, ["action"] = ex.Action }
            },

            ConcurrencyException ex => new ProblemDetails
            {
                Title = "Concurrency Conflict",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.Conflict,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                Extensions = { ["entityName"] = ex.EntityName, ["entityId"] = ex.EntityId?.ToString() }
            },

            ArgumentNullException ex => new ProblemDetails
            {
                Title = "Missing Required Parameter",
                Detail = $"The parameter '{ex.ParamName}' is required but was not provided.",
                Status = (int)HttpStatusCode.BadRequest,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Extensions = { ["parameterName"] = ex.ParamName }
            },

            ArgumentException ex => new ProblemDetails
            {
                Title = "Invalid Argument",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.BadRequest,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            },

            UnauthorizedAccessException => new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Authentication is required to access this resource.",
                Status = (int)HttpStatusCode.Unauthorized,
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
            },

            NotImplementedException => new ProblemDetails
            {
                Title = "Not Implemented",
                Detail = "This functionality is not yet implemented.",
                Status = (int)HttpStatusCode.NotImplemented,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.2"
            },

            TimeoutException => new ProblemDetails
            {
                Title = "Request Timeout",
                Detail = "The request timed out while processing.",
                Status = (int)HttpStatusCode.RequestTimeout,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.7"
            },

            _ => new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = _environment.IsDevelopment() 
                    ? $"{exception.Message}\n{exception.StackTrace}" 
                    : "An unexpected error occurred while processing your request.",
                Status = (int)HttpStatusCode.InternalServerError,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            }
        };

        // Add trace identifier for debugging
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        // Add timestamp
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;

        context.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(problemDetails, options);
        await context.Response.WriteAsync(json);
    }
}
