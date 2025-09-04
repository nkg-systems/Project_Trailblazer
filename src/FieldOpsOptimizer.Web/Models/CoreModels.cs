namespace FieldOpsOptimizer.Web.Models;

public class TechnicianDto
{
    public Guid Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public TechnicianStatus Status { get; set; }
    public List<string> Skills { get; set; } = new();
    public decimal HourlyRate { get; set; }
    public AddressDto? HomeAddress { get; set; }
    public CoordinateDto? CurrentLocation { get; set; }
    public DateTime? LastLocationUpdate { get; set; }
    public List<WorkingHoursDto> WorkingHours { get; set; } = new();
}

public class ServiceJobDto
{
    public Guid Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public AddressDto ServiceAddress { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ScheduledDate { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public TimeSpan? PreferredTimeWindow { get; set; }
    public JobStatus Status { get; set; }
    public JobPriority Priority { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public Guid? RouteId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class RouteDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public Guid AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public RouteStatus Status { get; set; }
    public double TotalDistanceKm { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<RouteStopDto> Stops { get; set; } = new();
    public bool IsOptimized { get; set; }
    public string? OptimizationAlgorithm { get; set; }
    public double EstimatedFuelSavings { get; set; }
    public TimeSpan EstimatedTimeSavings { get; set; }
}

public class RouteStopDto
{
    public Guid JobId { get; set; }
    public int SequenceOrder { get; set; }
    public TimeSpan EstimatedTravelTime { get; set; }
    public double DistanceFromPreviousKm { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ServiceJobDto? Job { get; set; }
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string FormattedAddress { get; set; } = string.Empty;
    public CoordinateDto? Coordinate { get; set; }
}

public class CoordinateDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class WorkingHoursDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

// Enums
public enum TechnicianStatus
{
    Active,
    Inactive,
    OnLeave
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
    Low,
    Medium,
    High,
    Emergency
}

public enum RouteStatus
{
    Draft,
    Optimized,
    InProgress,
    Completed
}
