using FieldOpsOptimizer.Application.Common.Interfaces;

namespace FieldOpsOptimizer.Application.Features.Technicians.Commands.CreateTechnician;

public record CreateTechnicianCommand(
    string EmployeeId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    decimal HourlyRate,
    List<string> Skills,
    string TenantId) : ICommand<CreateTechnicianResponse>;

public record CreateTechnicianResponse(
    Guid Id,
    string EmployeeId,
    string FullName,
    string Email);
