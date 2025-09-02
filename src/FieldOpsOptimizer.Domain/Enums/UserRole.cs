namespace FieldOpsOptimizer.Domain.Enums;

/// <summary>
/// Defines the roles available in the field operations system
/// </summary>
public enum UserRole
{
    /// <summary>
    /// System administrator with full access
    /// </summary>
    Admin = 1,
    
    /// <summary>
    /// Dispatcher who manages jobs and assignments
    /// </summary>
    Dispatcher = 2,
    
    /// <summary>
    /// Field technician who executes jobs
    /// </summary>
    Technician = 3,
    
    /// <summary>
    /// Customer who can view their jobs
    /// </summary>
    Customer = 4,
    
    /// <summary>
    /// Manager who can view reports and analytics
    /// </summary>
    Manager = 5
}
