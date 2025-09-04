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
            .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.JobType, opt => opt.MapFrom(src => src.JobType.ToString()))
            .ForMember(dest => dest.RequiredSkills, opt => opt.MapFrom(src => src.RequiredSkills.ToList()))
            .ForMember(dest => dest.AssignedTechnician, opt => opt.MapFrom(src => src.AssignedTechnician));
            
        CreateMap<CreateServiceJobDto, ServiceJob>();

        // Route mappings
        CreateMap<FieldOpsOptimizer.Domain.Entities.Route, RouteDto>()
            .ForMember(dest => dest.RouteName, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.TechnicianId, opt => opt.MapFrom(src => src.AssignedTechnicianId))
            .ForMember(dest => dest.RouteDate, opt => opt.MapFrom(src => src.ScheduledDate))
            .ForMember(dest => dest.Stops, opt => opt.MapFrom(src => src.RouteStops.OrderBy(rs => rs.SequenceOrder).ToList()));
            
        CreateMap<RouteStop, RouteStopDto>()
            .ForMember(dest => dest.StopOrder, opt => opt.MapFrom(src => src.SequenceOrder))
            .ForMember(dest => dest.ServiceJobId, opt => opt.MapFrom(src => src.JobId))
            .ForMember(dest => dest.DistanceFromPrevious, opt => opt.MapFrom(src => src.DistanceFromPreviousKm))
            .ForMember(dest => dest.TravelTimeFromPrevious, opt => opt.MapFrom(src => src.EstimatedTravelTime));

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
