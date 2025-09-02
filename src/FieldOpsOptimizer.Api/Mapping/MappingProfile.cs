using AutoMapper;
using FieldOpsOptimizer.Api.DTOs;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Application.Features.Technicians.Commands.CreateTechnician;

namespace FieldOpsOptimizer.Api.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Technician mappings
        CreateMap<Technician, TechnicianDto>()
            .ForMember(dest => dest.Skills, opt => opt.MapFrom(src => src.Skills.ToList()))
            .ForMember(dest => dest.WorkingHours, opt => opt.MapFrom(src => src.WorkingHours.ToList()));
            
        CreateMap<CreateTechnicianDto, CreateTechnicianCommand>();
        
        CreateMap<CreateTechnicianCommand, Technician>()
            .ConstructUsing(src => new Technician(
                src.EmployeeId,
                src.FirstName,
                src.LastName,
                src.Email,
                src.TenantId,
                src.HourlyRate));

        // Service Job mappings
        CreateMap<ServiceJob, ServiceJobDto>()
            .ForMember(dest => dest.RequiredSkills, opt => opt.MapFrom(src => src.RequiredSkills.ToList()));
            
        CreateMap<CreateServiceJobDto, ServiceJob>()
            .ConstructUsing(src => new ServiceJob(
                src.Title,
                src.Description,
                src.Priority,
                src.JobType,
                src.ScheduledDate,
                src.EstimatedDuration,
                src.RequiredSkills,
                new Address(src.ServiceAddress.Street, src.ServiceAddress.City, src.ServiceAddress.State, src.ServiceAddress.ZipCode, src.ServiceAddress.Country),
                new Coordinate(src.ServiceLatitude, src.ServiceLongitude),
                src.CustomerName,
                src.CustomerPhone,
                src.CustomerEmail,
                src.TenantId));

        // Route mappings
        CreateMap<Route, RouteDto>()
            .ForMember(dest => dest.Stops, opt => opt.MapFrom(src => src.RouteStops.OrderBy(rs => rs.StopOrder).ToList()));
            
        CreateMap<RouteStop, RouteStopDto>();

        // Value Object mappings
        CreateMap<Address, AddressDto>();
        CreateMap<AddressDto, Address>()
            .ConstructUsing(src => new Address(src.Street, src.City, src.State, src.ZipCode, src.Country));
            
        CreateMap<Coordinate, CoordinateDto>();
        CreateMap<CoordinateDto, Coordinate>()
            .ConstructUsing(src => new Coordinate(src.Latitude, src.Longitude));
            
        CreateMap<WorkingHours, WorkingHoursDto>();
        CreateMap<WorkingHoursDto, WorkingHours>()
            .ConstructUsing(src => new WorkingHours(src.DayOfWeek, src.StartTime, src.EndTime));
    }
}
