using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Api.DTOs;

public class TechnicianDto
{
    public Guid Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public TechnicianStatus Status { get; set; }
    public decimal HourlyRate { get; set; }
    public List<string> Skills { get; set; } = new();
    public AddressDto? HomeAddress { get; set; }
    public CoordinateDto? CurrentLocation { get; set; }
    public DateTime? LastLocationUpdate { get; set; }
    public List<WorkingHoursDto> WorkingHours { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateTechnicianDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public decimal HourlyRate { get; set; }
    public List<string> Skills { get; set; } = new();
    public string TenantId { get; set; } = string.Empty;
}

public class UpdateTechnicianDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public decimal HourlyRate { get; set; }
    public List<string> Skills { get; set; } = new();
    public TechnicianStatus Status { get; set; }
}

public class UpdateTechnicianLocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
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
