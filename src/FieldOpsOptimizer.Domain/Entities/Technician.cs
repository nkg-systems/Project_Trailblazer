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

    public IReadOnlyList<string> Skills => _skills.AsReadOnly();
    public IReadOnlyList<WorkingHours> WorkingHours => _workingHours.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

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
}

public record WorkingHours(DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime);
