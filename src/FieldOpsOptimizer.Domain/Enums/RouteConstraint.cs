namespace FieldOpsOptimizer.Domain.Enums;

/// <summary>
/// Types of constraints that can be applied during route optimization
/// </summary>
public enum RouteConstraint
{
    /// <summary>
    /// Respect job time windows and preferred arrival times
    /// </summary>
    TimeWindows = 1,

    /// <summary>
    /// Ensure technician skills match job requirements
    /// </summary>
    SkillMatching = 2,

    /// <summary>
    /// Respect technician working hours
    /// </summary>
    WorkingHours = 3,

    /// <summary>
    /// Limit maximum route duration
    /// </summary>
    MaxRouteDuration = 4,

    /// <summary>
    /// Limit maximum travel distance
    /// </summary>
    MaxTravelDistance = 5,

    /// <summary>
    /// Ensure jobs are scheduled on correct dates
    /// </summary>
    ScheduledDate = 6,

    /// <summary>
    /// Prevent overlapping jobs at same location
    /// </summary>
    NoOverlapping = 7,

    /// <summary>
    /// Consider vehicle capacity constraints
    /// </summary>
    VehicleCapacity = 8,

    /// <summary>
    /// Respect break time requirements
    /// </summary>
    BreakTimes = 9,

    /// <summary>
    /// Consider traffic and real-time conditions
    /// </summary>
    TrafficConditions = 10
}
