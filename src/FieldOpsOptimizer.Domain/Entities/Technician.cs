using FieldOpsOptimizer.Domain.Common;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Domain.Entities;

public class Technician : BaseEntity
{
    private readonly List<string> _skills = new();
    private readonly List<WorkingHours> _workingHours = new();

    public string EmployeeId { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public TechnicianStatus Status { get; private set; } = TechnicianStatus.Active;
    public Address? HomeAddress { get; private set; }
    public Coordinate? CurrentLocation { get; private set; }
    public DateTime? LastLocationUpdate { get; private set; }
    public decimal HourlyRate { get; private set; }
    public string TenantId { get; private set; } = string.Empty;

    // Enhanced Availability Features
    /// <summary>
    /// Whether the technician is currently available for new job assignments
    /// </summary>
    public bool IsCurrentlyAvailable { get; private set; } = true;

    /// <summary>
    /// When the technician's availability status was last changed
    /// </summary>
    public DateTime? AvailabilityChangedAt { get; private set; }

    /// <summary>
    /// User ID who last changed the technician's availability
    /// </summary>
    public Guid? AvailabilityChangedByUserId { get; private set; }

    /// <summary>
    /// Name of user who last changed the technician's availability
    /// </summary>
    public string? AvailabilityChangedByUserName { get; private set; }

    /// <summary>
    /// Reason for current unavailability (if applicable)
    /// </summary>
    public TechnicianAvailabilityReason? UnavailabilityReason { get; private set; }

    /// <summary>
    /// Additional notes about current availability status
    /// </summary>
    public string? AvailabilityNotes { get; private set; }

    /// <summary>
    /// Expected time when technician will be available again (if currently unavailable)
    /// </summary>
    public DateTime? ExpectedAvailableAt { get; private set; }

    /// <summary>
    /// Whether the technician can be assigned emergency jobs even when unavailable
    /// </summary>
    public bool CanTakeEmergencyJobs { get; private set; } = true;

    /// <summary>
    /// Maximum number of concurrent jobs this technician can handle
    /// </summary>
    public int MaxConcurrentJobs { get; private set; } = 3;

    /// <summary>
    /// Row version for optimistic concurrency control
    /// </summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyList<string> Skills => _skills.AsReadOnly();
    public IReadOnlyList<WorkingHours> WorkingHours => _workingHours.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

    // Navigation properties
    public virtual ICollection<ServiceJob> ServiceJobs { get; private set; } = new List<ServiceJob>();

    private Technician() { } // For EF Core

    public Technician(
        string employeeId,
        string firstName,
        string lastName,
        string email,
        string tenantId,
        decimal hourlyRate = 0)
    {
        EmployeeId = employeeId;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        TenantId = tenantId;
        HourlyRate = hourlyRate;
    }

    public void UpdateContactInfo(string firstName, string lastName, string email, string? phone = null)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        UpdateTimestamp();
    }

    public void UpdateLocation(Coordinate location, DateTime? timestamp = null)
    {
        CurrentLocation = location;
        LastLocationUpdate = timestamp ?? DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void SetHomeAddress(Address address)
    {
        HomeAddress = address;
        UpdateTimestamp();
    }

    public void AddSkill(string skill)
    {
        if (!_skills.Contains(skill, StringComparer.OrdinalIgnoreCase))
        {
            _skills.Add(skill);
            UpdateTimestamp();
        }
    }

    public void RemoveSkill(string skill)
    {
        _skills.RemoveAll(s => string.Equals(s, skill, StringComparison.OrdinalIgnoreCase));
        UpdateTimestamp();
    }

    public void SetWorkingHours(List<WorkingHours> workingHours)
    {
        _workingHours.Clear();
        _workingHours.AddRange(workingHours);
        UpdateTimestamp();
    }

    public void UpdateStatus(TechnicianStatus status)
    {
        Status = status;
        UpdateTimestamp();
    }

    public void UpdateHourlyRate(decimal hourlyRate)
    {
        HourlyRate = hourlyRate;
        UpdateTimestamp();
    }

    public bool IsAvailableAt(DateTime dateTime)
    {
        if (Status != TechnicianStatus.Active)
            return false;

        var dayOfWeek = dateTime.DayOfWeek;
        var timeOfDay = dateTime.TimeOfDay;

        return _workingHours.Any(wh => 
            wh.DayOfWeek == dayOfWeek && 
            wh.StartTime <= timeOfDay && 
            wh.EndTime >= timeOfDay);
    }

    /// <summary>
    /// Sets the technician's current availability status with audit trail
    /// </summary>
    /// <param name="isAvailable">Whether the technician is available</param>
    /// <param name="changedByUserId">ID of user making the change</param>
    /// <param name="changedByUserName">Name of user making the change</param>
    /// <param name="reason">Reason for unavailability (required if setting to unavailable)</param>
    /// <param name="notes">Additional notes about the availability change</param>
    /// <param name="expectedAvailableAt">When technician is expected to be available again</param>
    public void SetAvailability(
        bool isAvailable, 
        Guid changedByUserId, 
        string changedByUserName,
        TechnicianAvailabilityReason? reason = null,
        string? notes = null,
        DateTime? expectedAvailableAt = null)
    {
        if (changedByUserId == Guid.Empty)
            throw new ArgumentException("Changed by user ID cannot be empty", nameof(changedByUserId));

        if (string.IsNullOrWhiteSpace(changedByUserName))
            throw new ArgumentException("Changed by user name cannot be empty", nameof(changedByUserName));

        // Validate business rules
        if (!isAvailable && reason == null)
            throw new ArgumentException("Reason is required when setting technician as unavailable", nameof(reason));

        if (isAvailable && reason != null)
            throw new ArgumentException("Reason should not be provided when setting technician as available", nameof(reason));

        if (Status != TechnicianStatus.Active && isAvailable)
            throw new InvalidOperationException($"Cannot set inactive technician as available. Current status: {Status}");

        // Validate notes length
        if (!string.IsNullOrWhiteSpace(notes) && notes.Length > 500)
            throw new ArgumentException("Availability notes cannot exceed 500 characters", nameof(notes));

        // Update availability status
        var previousAvailability = IsCurrentlyAvailable;
        IsCurrentlyAvailable = isAvailable;
        AvailabilityChangedAt = DateTime.UtcNow;
        AvailabilityChangedByUserId = changedByUserId;
        AvailabilityChangedByUserName = changedByUserName.Trim();
        UnavailabilityReason = isAvailable ? null : reason;
        AvailabilityNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ExpectedAvailableAt = isAvailable ? null : expectedAvailableAt;

        UpdateTimestamp();

        // Log significant availability changes
        if (previousAvailability != isAvailable)
        {
            // This would typically trigger domain events for notifications
            // AddDomainEvent(new TechnicianAvailabilityChangedEvent(...));
        }
    }

    /// <summary>
    /// Sets the technician as unavailable with a specific reason
    /// </summary>
    /// <param name="reason">Reason for unavailability</param>
    /// <param name="changedByUserId">ID of user making the change</param>
    /// <param name="changedByUserName">Name of user making the change</param>
    /// <param name="notes">Additional notes</param>
    /// <param name="expectedAvailableAt">When technician is expected to be available again</param>
    public void SetUnavailable(
        TechnicianAvailabilityReason reason,
        Guid changedByUserId,
        string changedByUserName,
        string? notes = null,
        DateTime? expectedAvailableAt = null)
    {
        SetAvailability(false, changedByUserId, changedByUserName, reason, notes, expectedAvailableAt);
    }

    /// <summary>
    /// Sets the technician as available
    /// </summary>
    /// <param name="changedByUserId">ID of user making the change</param>
    /// <param name="changedByUserName">Name of user making the change</param>
    /// <param name="notes">Optional notes about becoming available</param>
    public void SetAvailable(Guid changedByUserId, string changedByUserName, string? notes = null)
    {
        SetAvailability(true, changedByUserId, changedByUserName, null, notes, null);
    }

    /// <summary>
    /// Updates the expected availability time for an unavailable technician
    /// </summary>
    /// <param name="expectedAvailableAt">New expected availability time</param>
    /// <param name="updatedByUserId">ID of user making the update</param>
    public void UpdateExpectedAvailability(DateTime? expectedAvailableAt, Guid updatedByUserId)
    {
        if (IsCurrentlyAvailable)
            throw new InvalidOperationException("Cannot set expected availability for an available technician");

        ExpectedAvailableAt = expectedAvailableAt;
        UpdateTimestamp();
    }

    /// <summary>
    /// Sets emergency job handling capability
    /// </summary>
    /// <param name="canTakeEmergencyJobs">Whether technician can take emergency jobs</param>
    /// <param name="updatedByUserId">ID of user making the change</param>
    public void SetEmergencyJobCapability(bool canTakeEmergencyJobs, Guid updatedByUserId)
    {
        CanTakeEmergencyJobs = canTakeEmergencyJobs;
        UpdateTimestamp();
    }

    /// <summary>
    /// Updates the maximum concurrent jobs this technician can handle
    /// </summary>
    /// <param name="maxJobs">Maximum concurrent jobs (1-10)</param>
    /// <param name="updatedByUserId">ID of user making the change</param>
    public void SetMaxConcurrentJobs(int maxJobs, Guid updatedByUserId)
    {
        if (maxJobs < 1 || maxJobs > 10)
            throw new ArgumentException("Max concurrent jobs must be between 1 and 10", nameof(maxJobs));

        MaxConcurrentJobs = maxJobs;
        UpdateTimestamp();
    }

    /// <summary>
    /// Determines if technician can take a new job based on availability and current workload
    /// </summary>
    /// <param name="isEmergencyJob">Whether this is an emergency job</param>
    /// <param name="currentJobCount">Current number of assigned jobs</param>
    /// <returns>True if technician can take the job</returns>
    public bool CanTakeNewJob(bool isEmergencyJob = false, int currentJobCount = 0)
    {
        // Must be active status
        if (Status != TechnicianStatus.Active)
            return false;

        // For emergency jobs, check emergency capability even if unavailable
        if (isEmergencyJob && CanTakeEmergencyJobs)
            return currentJobCount < MaxConcurrentJobs;

        // For regular jobs, must be currently available
        if (!IsCurrentlyAvailable)
            return false;

        // Check workload capacity
        return currentJobCount < MaxConcurrentJobs;
    }

    /// <summary>
    /// Gets a summary of the technician's current availability status
    /// </summary>
    /// <returns>Availability status summary</returns>
    public string GetAvailabilityStatusSummary()
    {
        if (Status != TechnicianStatus.Active)
            return $"Inactive ({Status})";

        if (!IsCurrentlyAvailable)
        {
            var summary = $"Unavailable - {UnavailabilityReason}";
            if (ExpectedAvailableAt.HasValue)
                summary += $" (Expected back: {ExpectedAvailableAt:HH:mm})";
            return summary;
        }

        return "Available";
    }

    /// <summary>
    /// Checks if the technician should automatically become available based on expected time
    /// </summary>
    /// <returns>True if should auto-become available</returns>
    public bool ShouldAutoResumeAvailability()
    {
        return !IsCurrentlyAvailable && 
               ExpectedAvailableAt.HasValue && 
               DateTime.UtcNow >= ExpectedAvailableAt.Value;
    }
}

public class WorkingHours : BaseEntity
{
    public new Guid Id { get; private set; }
    public Guid TechnicianId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    public bool IsAvailable { get; private set; } = true;

    // Navigation properties
    public Technician? Technician { get; private set; }

    private WorkingHours() { } // For EF Core

    public WorkingHours(DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime, Guid? technicianId = null)
    {
        Id = Guid.NewGuid();
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        if (technicianId.HasValue)
            TechnicianId = technicianId.Value;
    }

    public void SetAvailability(bool isAvailable)
    {
        IsAvailable = isAvailable;
        UpdateTimestamp();
    }

    public void UpdateHours(TimeSpan startTime, TimeSpan endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
        UpdateTimestamp();
    }
}
