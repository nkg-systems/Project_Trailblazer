namespace FieldOpsOptimizer.Domain.Enums;

public enum TechnicianStatus
{
    Active,
    Inactive,
    OnLeave,
    Terminated
}

public enum JobStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
    OnHold
}

public enum JobPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Emergency = 4
}

public enum RouteStatus
{
    Draft,
    Optimizing,
    Optimized,
    InProgress,
    Completed
}

public enum WeatherCondition
{
    Clear,
    Cloudy,
    Rain,
    Snow,
    Storm,
    Fog,
    Extreme
}

public enum OptimizationObjective
{
    MinimizeDistance,
    MinimizeTime,
    BalanceWorkload,
    MaximizeRevenue
}

public enum WeatherSeverity
{
    Mild = 1,
    Moderate = 2,
    Severe = 3,
    Extreme = 4
}

public enum JobType
{
    Installation,
    Maintenance,
    Repair,
    Inspection,
    Emergency,
    Consultation,
    Other
}

/// <summary>
/// Types of notes that can be added to service jobs
/// </summary>
public enum JobNoteType
{
    /// <summary>
    /// General note visible to all internal users
    /// </summary>
    General = 1,
    
    /// <summary>
    /// Internal note visible only to staff members
    /// </summary>
    Internal = 2,
    
    /// <summary>
    /// Customer communication note
    /// </summary>
    CustomerCommunication = 3,
    
    /// <summary>
    /// System-generated note for status changes
    /// </summary>
    StatusChange = 4,
    
    /// <summary>
    /// System-generated note for assignment changes
    /// </summary>
    Assignment = 5,
    
    /// <summary>
    /// Technical details and troubleshooting information
    /// </summary>
    Technical = 6
}

/// <summary>
/// Reasons why a technician might be unavailable
/// </summary>
public enum TechnicianAvailabilityReason
{
    /// <summary>
    /// Technician is on a scheduled break
    /// </summary>
    OnBreak = 1,
    
    /// <summary>
    /// Technician is at lunch
    /// </summary>
    AtLunch = 2,
    
    /// <summary>
    /// Technician is sick or on medical leave
    /// </summary>
    Sick = 3,
    
    /// <summary>
    /// Technician is traveling between jobs
    /// </summary>
    InTransit = 4,
    
    /// <summary>
    /// Technician is handling an emergency call
    /// </summary>
    OnEmergencyCall = 5,
    
    /// <summary>
    /// Technician is off duty
    /// </summary>
    OffDuty = 6,
    
    /// <summary>
    /// Administrative duties (paperwork, training, etc.)
    /// </summary>
    Administrative = 7,
    
    /// <summary>
    /// Equipment or vehicle maintenance
    /// </summary>
    Equipment = 8,
    
    /// <summary>
    /// Other reason (should specify in notes)
    /// </summary>
    Other = 99
}

// Alias for JobStatus to maintain compatibility with API controllers
public enum ServiceJobStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
    OnHold = 4
}
