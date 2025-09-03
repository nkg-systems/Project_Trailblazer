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
