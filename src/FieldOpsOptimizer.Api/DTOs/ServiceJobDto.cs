using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Api.DTOs;

public class ServiceJobDto
{
    public Guid Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public JobPriority Priority { get; set; }
    public string JobType { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public TimeSpan? ActualDuration { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public AddressDto ServiceAddress { get; set; } = new();
    public CoordinateDto ServiceLocation { get; set; } = new();
    public Guid? AssignedTechnicianId { get; set; }
    public TechnicianDto? AssignedTechnician { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

public class CreateServiceJobDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JobPriority Priority { get; set; }
    public string JobType { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public AddressDto ServiceAddress { get; set; } = new();
    public double ServiceLatitude { get; set; }
    public double ServiceLongitude { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal EstimatedCost { get; set; }
    public string TenantId { get; set; } = string.Empty;
}

public class UpdateServiceJobDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public JobPriority Priority { get; set; }
    public DateTime ScheduledDate { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public List<string> RequiredSkills { get; set; } = new();
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal EstimatedCost { get; set; }
}

public class AssignTechnicianDto
{
    public Guid TechnicianId { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string? Notes { get; set; }
}

public class UpdateJobStatusDto
{
    public JobStatus Status { get; set; }
    public string? Notes { get; set; }
    public TimeSpan? ActualDuration { get; set; }
    public decimal? ActualCost { get; set; }
}
