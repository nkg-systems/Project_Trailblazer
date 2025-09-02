# Global Exception Handling System

## Overview

The FieldOpsOptimizer API now includes a comprehensive global exception handling system that provides consistent, user-friendly error responses while following RFC 7807 (Problem Details for HTTP APIs) standards.

## Components

### 1. Custom Domain Exceptions

**Location**: `src/FieldOpsOptimizer.Domain/Exceptions/DomainException.cs`

The system includes several custom exception types that represent specific domain and application errors:

#### Base Exception
- **`DomainException`**: Abstract base class for all domain-related exceptions

#### Specific Exceptions
- **`EntityNotFoundException`**: When a requested entity is not found
- **`ValidationException`**: When input validation fails
- **`BusinessRuleValidationException`**: When business rules are violated
- **`InvalidEntityStateException`**: When an operation is invalid for the current entity state
- **`DuplicateEntityException`**: When trying to create duplicate entities
- **`AccessDeniedException`**: When access to a resource is denied
- **`ConcurrencyException`**: When concurrent updates conflict

### 2. Global Exception Handling Middleware

**Location**: `src/FieldOpsOptimizer.Api/Middleware/GlobalExceptionHandlingMiddleware.cs`

This middleware catches all unhandled exceptions and converts them to appropriate HTTP responses with consistent structure.

#### Features:
- **Automatic HTTP Status Code Mapping**: Each exception type maps to appropriate HTTP status codes
- **RFC 7807 Compliance**: Returns problem details in standardized format
- **Detailed Error Information**: Includes additional context for debugging
- **Environment-Sensitive Details**: Shows more information in development environment
- **Comprehensive Logging**: All exceptions are logged with context

### 3. Problem Details Factory

**Location**: `src/FieldOpsOptimizer.Api/Common/ProblemDetailsFactory.cs`

Custom factory for creating RFC 7807 compliant problem details responses, including:
- Trace identifiers for request tracking
- Timestamps for audit trails
- Request path and method information
- Development environment debugging headers

### 4. Exception Mapping

| Exception Type | HTTP Status | Title | Additional Data |
|---------------|-------------|--------|-----------------|
| `EntityNotFoundException` | 404 Not Found | Resource Not Found | entityName, entityId |
| `ValidationException` | 400 Bad Request | Validation Error | errors (field-level details) |
| `BusinessRuleValidationException` | 400 Bad Request | Business Rule Violation | ruleName |
| `InvalidEntityStateException` | 409 Conflict | Invalid Entity State | entityName, currentState, requestedOperation |
| `DuplicateEntityException` | 409 Conflict | Duplicate Entity | entityName, duplicateProperty, duplicateValue |
| `AccessDeniedException` | 403 Forbidden | Access Denied | resource, action |
| `ConcurrencyException` | 409 Conflict | Concurrency Conflict | entityName, entityId |
| `ArgumentNullException` | 400 Bad Request | Missing Required Parameter | parameterName |
| `ArgumentException` | 400 Bad Request | Invalid Argument | - |
| `UnauthorizedAccessException` | 401 Unauthorized | Unauthorized | - |
| `NotImplementedException` | 501 Not Implemented | Not Implemented | - |
| `TimeoutException` | 408 Request Timeout | Request Timeout | - |
| Generic `Exception` | 500 Internal Server Error | Internal Server Error | Stack trace (dev only) |

## Usage Examples

### In Controllers

Replace manual error handling with custom exceptions:

```csharp
// Before (manual error handling)
if (job == null)
{
    return NotFound(new ProblemDetails
    {
        Title = "Job not found",
        Detail = $"Service job with ID {id} was not found"
    });
}

// After (using custom exceptions)
if (job == null)
{
    throw new EntityNotFoundException(nameof(ServiceJob), id);
}
```

### Validation Errors

```csharp
// Single field validation
throw new ValidationException("email", "Invalid email format");

// Multiple field validation
var errors = new Dictionary<string, string[]>
{
    ["firstName"] = new[] { "First name is required" },
    ["email"] = new[] { "Invalid email format", "Email is already in use" }
};
throw new ValidationException(errors);
```

### Business Rule Violations

```csharp
if (job.Status == JobStatus.Completed)
{
    throw new InvalidEntityStateException(
        nameof(ServiceJob), 
        job.Status.ToString(), 
        "delete");
}

if (missingSkills.Any())
{
    throw new BusinessRuleValidationException(
        "TechnicianSkillsValidation",
        $"Technician is missing required skills: {string.Join(", ", missingSkills)}");
}
```

## Error Response Format

All error responses follow the RFC 7807 Problem Details format:

```json
{
  "title": "Resource Not Found",
  "detail": "ServiceJob with id 'f47ac10b-58cc-4372-a567-0e02b2c3d479' was not found.",
  "status": 404,
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "entityName": "ServiceJob",
  "entityId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "traceId": "0HN7KOKDH9O2J:00000001",
  "timestamp": "2024-01-15T10:30:45.123Z"
}
```

### Validation Error Example

```json
{
  "title": "Validation Error",
  "detail": "One or more validation errors occurred.",
  "status": 400,
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "errors": {
    "firstName": ["First name is required"],
    "email": ["Invalid email format", "Email is already in use"]
  },
  "traceId": "0HN7KOKDH9O2J:00000002",
  "timestamp": "2024-01-15T10:30:45.456Z"
}
```

## Testing Exception Handling

A test controller is available in debug builds for testing all exception types:

**Base URL**: `/api/test/exceptions/`

### Test Endpoints:
- `GET /entity-not-found` - Tests EntityNotFoundException
- `GET /validation-error` - Tests ValidationException
- `GET /business-rule-violation` - Tests BusinessRuleValidationException
- `GET /invalid-entity-state` - Tests InvalidEntityStateException
- `GET /duplicate-entity` - Tests DuplicateEntityException
- `GET /access-denied` - Tests AccessDeniedException
- `GET /concurrency-conflict` - Tests ConcurrencyException
- `GET /argument-null` - Tests ArgumentNullException
- `GET /argument-invalid` - Tests ArgumentException
- `GET /unauthorized` - Tests UnauthorizedAccessException
- `GET /not-implemented` - Tests NotImplementedException
- `GET /timeout` - Tests TimeoutException
- `GET /generic-error` - Tests generic exception handling

## Configuration

Exception handling is automatically configured in `Program.cs`:

```csharp
// Configure global exception handling
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<ProblemDetailsFactory, CustomProblemDetailsFactory>();

// Configure API behavior for automatic model validation
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = false;
    options.SuppressMapClientErrors = false;
});

// Add middleware to pipeline
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
```

## Best Practices

### 1. Use Specific Exceptions
Always use the most specific exception type available:

```csharp
// Good
throw new EntityNotFoundException(nameof(ServiceJob), jobId);

// Less specific
throw new ArgumentException("Job not found");
```

### 2. Provide Meaningful Messages
Include context that helps users understand and resolve the issue:

```csharp
// Good
throw new BusinessRuleValidationException(
    "TechnicianAvailability", 
    "Technician is already assigned to another job at this time");

// Less helpful
throw new BusinessRuleValidationException("ValidationFailed", "Error");
```

### 3. Don't Catch and Re-throw
Let the middleware handle exceptions rather than catching and re-throwing:

```csharp
// Good
public async Task<ServiceJob> GetJobAsync(Guid id)
{
    var job = await _repository.GetByIdAsync(id);
    if (job == null)
        throw new EntityNotFoundException(nameof(ServiceJob), id);
    
    return job;
}

// Avoid
public async Task<ActionResult<ServiceJob>> GetJob(Guid id)
{
    try 
    {
        var job = await GetJobAsync(id);
        return Ok(job);
    }
    catch (EntityNotFoundException)
    {
        return NotFound(); // Loses exception details
    }
}
```

### 4. Log Context
The middleware automatically logs exceptions, but add context logging where helpful:

```csharp
_logger.LogWarning("Failed to assign job {JobId} to technician {TechnicianId}: missing skills", 
    jobId, technicianId);
throw new BusinessRuleValidationException(
    "TechnicianSkillsValidation",
    $"Technician lacks required skills: {string.Join(", ", missingSkills)}");
```

## Development vs Production

### Development Environment
- Full exception details in responses
- Stack traces included for debugging
- Additional request headers in problem details
- Detailed logging

### Production Environment
- Generic error messages for security
- No stack traces exposed
- Minimal debugging information
- Structured logging for monitoring

## Integration with Monitoring

The exception handling system integrates well with monitoring solutions:

- **Trace IDs**: Each error response includes a unique trace identifier
- **Structured Logging**: All exceptions are logged with context
- **HTTP Status Codes**: Proper status codes for monitoring alerts
- **Response Metrics**: Consistent format for error rate tracking

## Security Considerations

- **Information Disclosure**: Production mode limits sensitive information
- **Input Validation**: Validation exceptions prevent malicious input processing
- **Access Control**: Access denied exceptions don't reveal resource structure
- **Rate Limiting**: Consider implementing rate limiting for error-prone endpoints

The global exception handling system ensures that your API provides consistent, professional error responses while maintaining security and providing excellent developer experience for debugging and monitoring.
