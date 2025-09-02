using FieldOpsOptimizer.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FieldOpsOptimizer.Api.DTOs;

/// <summary>
/// DTO for representing availability time slots
/// </summary>
public class AvailabilitySlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public Guid TechnicianId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating technician working hours
/// </summary>
public class WorkingHoursUpdateDto
{
    [Required]
    public DayOfWeek DayOfWeek { get; set; }
    
    [Required]
    public TimeSpan StartTime { get; set; }
    
    [Required]
    public TimeSpan EndTime { get; set; }
    
    public bool IsAvailable { get; set; } = true;
}


/// <summary>
/// DTO for representing schedule conflicts
/// </summary>
public class ScheduleConflictDto
{
    public Guid TechnicianId { get; set; }
    public string ConflictType { get; set; } = string.Empty;
    public DateTime ConflictDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid Job1Id { get; set; }
    public Guid? Job2Id { get; set; }
    public string? Resolution { get; set; }
    public ConflictSeverity Severity { get; set; } = ConflictSeverity.Medium;
}

/// <summary>
/// DTO for requesting schedule summary
/// </summary>
public class ScheduleSummaryRequestDto
{
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    public List<Guid>? TechnicianIds { get; set; }
    
    public TechnicianStatus? Status { get; set; }
    
    public bool IncludeUtilization { get; set; } = true;
    
    public bool IncludeConflicts { get; set; } = false;
}

/// <summary>
/// DTO for technician schedule summary
/// </summary>
public class TechnicianScheduleSummaryDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int PendingJobs { get; set; }
    public int CancelledJobs { get; set; }
    public double TotalEstimatedHours { get; set; }
    public double UtilizationRate { get; set; }
    public int ConflictCount { get; set; }
    public DateTime? NextAvailableSlot { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// DTO for bulk schedule operations
/// </summary>
public class BulkScheduleUpdateDto
{
    [Required]
    public List<Guid> TechnicianIds { get; set; } = new();
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    public ScheduleOperation Operation { get; set; }
    
    public object? OperationData { get; set; }
}

/// <summary>
/// DTO for schedule optimization request
/// </summary>
public class ScheduleOptimizationRequestDto
{
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    public List<Guid>? TechnicianIds { get; set; }
    
    public List<Guid>? JobIds { get; set; }
    
    public OptimizationObjective Objective { get; set; } = OptimizationObjective.MinimizeTravelTime;
    
    public bool AllowRescheduling { get; set; } = true;
    
    public bool RespectTechnicianPreferences { get; set; } = true;
    
    public int MaxIterations { get; set; } = 1000;
}

/// <summary>
/// DTO for schedule optimization result
/// </summary>
public class ScheduleOptimizationResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<TechnicianScheduleDto> OptimizedSchedules { get; set; } = new();
    public OptimizationMetricsDto Metrics { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime OptimizationCompletedAt { get; set; }
}

/// <summary>
/// DTO for individual technician schedule in optimization result
/// </summary>
public class TechnicianScheduleDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public List<ScheduledJobDto> ScheduledJobs { get; set; } = new();
    public double TotalTravelTime { get; set; }
    public double TotalWorkingHours { get; set; }
    public double UtilizationRate { get; set; }
    public int ConflictCount { get; set; }
}

/// <summary>
/// DTO for scheduled job in optimization result
/// </summary>
public class ScheduledJobDto
{
    public Guid JobId { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public DateTime OriginalScheduledDate { get; set; }
    public DateTime OptimizedScheduledDate { get; set; }
    public bool WasRescheduled { get; set; }
    public string? RescheduleReason { get; set; }
    public double EstimatedTravelTime { get; set; }
    public CoordinateDto? Location { get; set; }
}

/// <summary>
/// DTO for optimization metrics
/// </summary>
public class OptimizationMetricsDto
{
    public double TotalTravelTimeReduction { get; set; }
    public double AverageUtilizationImprovement { get; set; }
    public int ConflictsResolved { get; set; }
    public int JobsRescheduled { get; set; }
    public TimeSpan OptimizationDuration { get; set; }
    public int IterationsCompleted { get; set; }
    public double FinalObjectiveScore { get; set; }
}

/// <summary>
/// Enums for schedule operations
/// </summary>
public enum ConflictSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ScheduleOperation
{
    Block,
    Unblock,
    UpdateWorkingHours,
    BulkReschedule,
    ApplyTemplate
}

