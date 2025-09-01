using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Entities;

namespace FieldOpsOptimizer.Application.Features.Technicians.Commands.CreateTechnician;

public class CreateTechnicianCommandHandler : ICommandHandler<CreateTechnicianCommand, CreateTechnicianResponse>
{
    private readonly IRepository<Technician> _technicianRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTechnicianCommandHandler(
        IRepository<Technician> technicianRepository,
        IUnitOfWork unitOfWork)
    {
        _technicianRepository = technicianRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateTechnicianResponse> Handle(
        CreateTechnicianCommand request, 
        CancellationToken cancellationToken)
    {
        // Check if technician with same employee ID already exists
        var existingTechnician = await _technicianRepository.FirstOrDefaultAsync(
            t => t.EmployeeId == request.EmployeeId && t.TenantId == request.TenantId,
            cancellationToken);

        if (existingTechnician != null)
        {
            throw new InvalidOperationException($"Technician with employee ID '{request.EmployeeId}' already exists.");
        }

        // Create new technician
        var technician = new Technician(
            request.EmployeeId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.TenantId,
            request.HourlyRate);

        // Add skills
        foreach (var skill in request.Skills)
        {
            technician.AddSkill(skill);
        }

        // Update phone if provided
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            technician.UpdateContactInfo(request.FirstName, request.LastName, request.Email, request.Phone);
        }

        _technicianRepository.Add(technician);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateTechnicianResponse(
            technician.Id,
            technician.EmployeeId,
            technician.FullName,
            technician.Email);
    }
}
