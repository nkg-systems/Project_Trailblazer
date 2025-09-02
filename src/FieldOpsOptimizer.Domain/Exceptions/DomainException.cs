namespace FieldOpsOptimizer.Domain.Exceptions;

/// <summary>
/// Base exception for domain-related errors
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    protected DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when an entity is not found
/// </summary>
public class EntityNotFoundException : DomainException
{
    public string EntityName { get; }
    public object EntityId { get; }

    public EntityNotFoundException(string entityName, object entityId)
        : base($"{entityName} with id '{entityId}' was not found.")
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}

/// <summary>
/// Exception thrown when a business rule is violated
/// </summary>
public class BusinessRuleValidationException : DomainException
{
    public string RuleName { get; }

    public BusinessRuleValidationException(string ruleName, string message)
        : base(message)
    {
        RuleName = ruleName;
    }
}

/// <summary>
/// Exception thrown when an entity is in an invalid state for the requested operation
/// </summary>
public class InvalidEntityStateException : DomainException
{
    public string EntityName { get; }
    public string CurrentState { get; }
    public string RequestedOperation { get; }

    public InvalidEntityStateException(string entityName, string currentState, string requestedOperation)
        : base($"Cannot perform '{requestedOperation}' on {entityName} in state '{currentState}'.")
    {
        EntityName = entityName;
        CurrentState = currentState;
        RequestedOperation = requestedOperation;
    }
}

/// <summary>
/// Exception thrown when a duplicate entity is detected
/// </summary>
public class DuplicateEntityException : DomainException
{
    public string EntityName { get; }
    public string DuplicateProperty { get; }
    public object DuplicateValue { get; }

    public DuplicateEntityException(string entityName, string duplicateProperty, object duplicateValue)
        : base($"{entityName} with {duplicateProperty} '{duplicateValue}' already exists.")
    {
        EntityName = entityName;
        DuplicateProperty = duplicateProperty;
        DuplicateValue = duplicateValue;
    }
}

/// <summary>
/// Exception thrown when access to a resource is denied
/// </summary>
public class AccessDeniedException : DomainException
{
    public string Resource { get; }
    public string Action { get; }

    public AccessDeniedException(string resource, string action)
        : base($"Access denied to {action} on {resource}.")
    {
        Resource = resource;
        Action = action;
    }
}

/// <summary>
/// Exception thrown when a validation error occurs
/// </summary>
public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string propertyName, string errorMessage)
        : base($"Validation failed for {propertyName}: {errorMessage}")
    {
        Errors = new Dictionary<string, string[]>
        {
            { propertyName, new[] { errorMessage } }
        };
    }
}

/// <summary>
/// Exception thrown when a concurrency conflict occurs
/// </summary>
public class ConcurrencyException : DomainException
{
    public string EntityName { get; }
    public object EntityId { get; }

    public ConcurrencyException(string entityName, object entityId)
        : base($"Concurrency conflict occurred while updating {entityName} with id '{entityId}'. The entity may have been modified by another user.")
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}
