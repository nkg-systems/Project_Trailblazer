using Microsoft.AspNetCore.Mvc;
using FieldOpsOptimizer.Domain.Exceptions;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// Test controller for demonstrating exception handling
/// This controller should be removed in production
/// </summary>
#if DEBUG
[ApiController]
[Route("api/test/exceptions")]
[Produces("application/json")]
public class TestExceptionController : ControllerBase
{
    /// <summary>
    /// Tests EntityNotFoundException handling
    /// </summary>
    [HttpGet("entity-not-found")]
    public IActionResult TestEntityNotFound()
    {
        throw new EntityNotFoundException("TestEntity", Guid.NewGuid());
    }

    /// <summary>
    /// Tests ValidationException handling
    /// </summary>
    [HttpGet("validation-error")]
    public IActionResult TestValidationError()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["firstName"] = new[] { "First name is required" },
            ["email"] = new[] { "Invalid email format", "Email is already in use" }
        };
        throw new ValidationException(errors);
    }

    /// <summary>
    /// Tests BusinessRuleValidationException handling
    /// </summary>
    [HttpGet("business-rule-violation")]
    public IActionResult TestBusinessRuleViolation()
    {
        throw new BusinessRuleValidationException("TestRule", "Test business rule has been violated");
    }

    /// <summary>
    /// Tests InvalidEntityStateException handling
    /// </summary>
    [HttpGet("invalid-entity-state")]
    public IActionResult TestInvalidEntityState()
    {
        throw new InvalidEntityStateException("ServiceJob", "Completed", "delete");
    }

    /// <summary>
    /// Tests DuplicateEntityException handling
    /// </summary>
    [HttpGet("duplicate-entity")]
    public IActionResult TestDuplicateEntity()
    {
        throw new DuplicateEntityException("User", "email", "test@example.com");
    }

    /// <summary>
    /// Tests AccessDeniedException handling
    /// </summary>
    [HttpGet("access-denied")]
    public IActionResult TestAccessDenied()
    {
        throw new AccessDeniedException("sensitive-data", "read");
    }

    /// <summary>
    /// Tests ConcurrencyException handling
    /// </summary>
    [HttpGet("concurrency-conflict")]
    public IActionResult TestConcurrencyConflict()
    {
        throw new ConcurrencyException("ServiceJob", Guid.NewGuid());
    }

    /// <summary>
    /// Tests ArgumentNullException handling
    /// </summary>
    [HttpGet("argument-null")]
    public IActionResult TestArgumentNull()
    {
        throw new ArgumentNullException("testParameter");
    }

    /// <summary>
    /// Tests ArgumentException handling
    /// </summary>
    [HttpGet("argument-invalid")]
    public IActionResult TestArgumentInvalid()
    {
        throw new ArgumentException("Invalid argument provided");
    }

    /// <summary>
    /// Tests UnauthorizedAccessException handling
    /// </summary>
    [HttpGet("unauthorized")]
    public IActionResult TestUnauthorized()
    {
        throw new UnauthorizedAccessException();
    }

    /// <summary>
    /// Tests NotImplementedException handling
    /// </summary>
    [HttpGet("not-implemented")]
    public IActionResult TestNotImplemented()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tests TimeoutException handling
    /// </summary>
    [HttpGet("timeout")]
    public IActionResult TestTimeout()
    {
        throw new TimeoutException();
    }

    /// <summary>
    /// Tests generic exception handling
    /// </summary>
    [HttpGet("generic-error")]
    public IActionResult TestGenericError()
    {
        throw new InvalidOperationException("This is a generic error for testing");
    }
}
#endif
