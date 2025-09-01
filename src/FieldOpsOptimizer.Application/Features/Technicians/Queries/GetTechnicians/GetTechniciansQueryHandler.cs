using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Entities;

namespace FieldOpsOptimizer.Application.Features.Technicians.Queries.GetTechnicians;

public class GetTechniciansQueryHandler : IQueryHandler<GetTechniciansQuery, GetTechniciansResponse>
{
    private readonly IRepository<Technician> _technicianRepository;

    public GetTechniciansQueryHandler(IRepository<Technician> technicianRepository)
    {
        _technicianRepository = technicianRepository;
    }

    public async Task<GetTechniciansResponse> Handle(GetTechniciansQuery request, CancellationToken cancellationToken)
    {
        // Build the query predicate
        var predicate = BuildPredicate(request);
        
        // Get technicians matching the criteria
        var technicians = await _technicianRepository.FindAsync(predicate, cancellationToken);
        var techniciansList = technicians.ToList();
        
        // Apply pagination
        var pagedTechnicians = techniciansList
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Map to DTOs
        var technicianDtos = pagedTechnicians.Select(t => new TechnicianDto(
            t.Id,
            t.EmployeeId,
            t.FirstName,
            t.LastName,
            t.FullName,
            t.Email,
            t.Phone,
            t.Status,
            t.Skills.ToList(),
            t.HourlyRate,
            t.CreatedAt)).ToList();

        return new GetTechniciansResponse(
            technicianDtos,
            techniciansList.Count,
            request.PageNumber,
            request.PageSize);
    }

    private static System.Linq.Expressions.Expression<Func<Technician, bool>> BuildPredicate(GetTechniciansQuery request)
    {
        var predicate = System.Linq.Expressions.Expression.Parameter(typeof(Technician), "t");
        
        // Always filter by tenant
        var tenantFilter = System.Linq.Expressions.Expression.Equal(
            System.Linq.Expressions.Expression.Property(predicate, nameof(Technician.TenantId)),
            System.Linq.Expressions.Expression.Constant(request.TenantId));

        System.Linq.Expressions.Expression filter = tenantFilter;

        // Add status filter if specified
        if (request.Status.HasValue)
        {
            var statusFilter = System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Property(predicate, nameof(Technician.Status)),
                System.Linq.Expressions.Expression.Constant(request.Status.Value));
            
            filter = System.Linq.Expressions.Expression.AndAlso(filter, statusFilter);
        }

        // Add skill filter if specified
        if (request.Skills?.Any() == true)
        {
            // For simplicity, we'll filter technicians that have ALL the requested skills
            foreach (var skill in request.Skills)
            {
                var skillProperty = System.Linq.Expressions.Expression.Property(predicate, nameof(Technician.Skills));
                var containsMethod = typeof(System.Collections.Generic.IReadOnlyList<string>).GetMethod("Contains")!;
                var skillFilter = System.Linq.Expressions.Expression.Call(
                    skillProperty,
                    containsMethod,
                    System.Linq.Expressions.Expression.Constant(skill));
                
                filter = System.Linq.Expressions.Expression.AndAlso(filter, skillFilter);
            }
        }

        return System.Linq.Expressions.Expression.Lambda<Func<Technician, bool>>(filter, predicate);
    }
}
