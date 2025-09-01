using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Application.Features.Technicians.Queries.GetTechnicians;

public record GetTechniciansQuery(
    string TenantId,
    TechnicianStatus? Status = null,
    List<string>? Skills = null,
    int PageNumber = 1,
    int PageSize = 10) : IQuery<GetTechniciansResponse>;

public record GetTechniciansResponse(
    List<TechnicianDto> Technicians,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record TechnicianDto(
    Guid Id,
    string EmployeeId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    TechnicianStatus Status,
    List<string> Skills,
    decimal HourlyRate,
    DateTime CreatedAt);
