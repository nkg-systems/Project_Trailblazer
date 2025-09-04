using FieldOpsOptimizer.Domain.Common;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Domain.Entities;

public class ServiceJob : BaseEntity
{
    private readonly List<string> _requiredSkills = new();
    private readonly List<string> _tags = new();

    public string JobNumber { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public string? CustomerPhone { get; private set; }
    public string? CustomerEmail { get; private set; }
    public Address ServiceAddress { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public JobStatus Status { get; private set; } = JobStatus.Scheduled;
    public JobPriority Priority { get; private set; } = JobPriority.Medium;
    public JobType JobType { get; private set; } = JobType.Other;
    public DateTime ScheduledDate { get; private set; }
    public TimeSpan? PreferredTimeWindow { get; private set; }
    public TimeSpan EstimatedDuration { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? AssignedTechnicianId { get; private set; }
    public Guid? RouteId { get; private set; }
    public decimal EstimatedRevenue { get; private set; }
    public decimal EstimatedCost { get; private set; }
    public string TenantId { get; private set; } = string.Empty;

    public IReadOnlyList<string> RequiredSkills => _requiredSkills.AsReadOnly();
    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    // Navigation properties
    public Technician? AssignedTechnician { get; private set; }
    public Route? Route { get; private set; }

    private ServiceJob() { } // For EF Core

    public ServiceJob(
        string jobNumber,
        string customerName,
        Address serviceAddress,
        string description,
        DateTime scheduledDate,
        TimeSpan estimatedDuration,
        string tenantId,
        JobPriority priority = JobPriority.Medium)
    {
        JobNumber = jobNumber;
        CustomerName = customerName;
        ServiceAddress = serviceAddress;
        Description = description;
        ScheduledDate = scheduledDate;
        EstimatedDuration = estimatedDuration;
        TenantId = tenantId;
        Priority = priority;
    }

    public void UpdateCustomerInfo(string customerName, string? phone = null, string? email = null)
    {
        CustomerName = customerName;
        CustomerPhone = phone;
        CustomerEmail = email;
        UpdateTimestamp();
    }

    public void UpdateServiceDetails(string description, string? notes = null)
    {
        Description = description;
        Notes = notes;
        UpdateTimestamp();
    }

    public void UpdateSchedule(DateTime scheduledDate, TimeSpan? preferredTimeWindow = null)
    {
        ScheduledDate = scheduledDate;
        PreferredTimeWindow = preferredTimeWindow;
        UpdateTimestamp();
    }

    public void AssignTechnician(Guid technicianId)
    {
        AssignedTechnicianId = technicianId;
        UpdateTimestamp();
    }

    public void UnassignTechnician()
    {
        AssignedTechnicianId = null;
        UpdateTimestamp();
    }

    public void AssignToRoute(Guid routeId)
    {
        RouteId = routeId;
        UpdateTimestamp();
    }

    public void UpdateStatus(JobStatus status)
    {
        Status = status;
        if (status == JobStatus.Completed)
        {
            CompletedAt = DateTime.UtcNow;
        }
        UpdateTimestamp();
    }

    public void UpdatePriority(JobPriority priority)
    {
        Priority = priority;
        UpdateTimestamp();
    }

    public void UpdateJobType(JobType jobType)
    {
        JobType = jobType;
        UpdateTimestamp();
    }

    public void UpdateEstimates(decimal estimatedRevenue, decimal estimatedCost)
    {
        EstimatedRevenue = estimatedRevenue;
        EstimatedCost = estimatedCost;
        UpdateTimestamp();
    }

    public void AddRequiredSkill(string skill)
    {
        if (!_requiredSkills.Contains(skill, StringComparer.OrdinalIgnoreCase))
        {
            _requiredSkills.Add(skill);
            UpdateTimestamp();
        }
    }

    public void RemoveRequiredSkill(string skill)
    {
        _requiredSkills.RemoveAll(s => string.Equals(s, skill, StringComparison.OrdinalIgnoreCase));
        UpdateTimestamp();
    }

    public void AddTag(string tag)
    {
        if (!_tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            _tags.Add(tag);
            UpdateTimestamp();
        }
    }

    public void RemoveTag(string tag)
    {
        _tags.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        UpdateTimestamp();
    }

    public bool RequiresSkills(IEnumerable<string> technicianSkills)
    {
        return _requiredSkills.All(requiredSkill =>
            technicianSkills.Any(techSkill =>
                string.Equals(techSkill, requiredSkill, StringComparison.OrdinalIgnoreCase)));
    }

    public double GetDistanceToInKilometers(Coordinate coordinate)
    {
        return ServiceAddress.Coordinate?.DistanceToInKilometers(coordinate) ?? double.MaxValue;
    }
}
