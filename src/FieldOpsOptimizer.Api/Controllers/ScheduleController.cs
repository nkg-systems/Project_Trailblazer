using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Api.DTOs;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// Controller for managing technician schedules and availability
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScheduleController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<ScheduleController> _logger;

    public ScheduleController(ApplicationDbContext context, IMapper mapper, ILogger<ScheduleController> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Get technician schedules with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TechnicianDto>>>> GetSchedules(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? technicianId,
        [FromQuery] TechnicianStatus? status,
        [FromQuery] string? location,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "ScheduledDate",
        [FromQuery] bool sortDescending = false)
    {
        try
        {
            var query = _context.Technicians
                .Include(t => t.ServiceJobs)
                .Include(t => t.WorkingHours)
                .AsQueryable();

            // Apply filters
            if (technicianId.HasValue)
                query = query.Where(t => t.Id == technicianId.Value);

            if (status.HasValue)
                query = query.Where(t => t.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(location))
                query = query.Where(t => t.BaseLocation != null && 
                    EF.Functions.ILike(t.BaseLocation, $"%{location}%"));

            // Filter by scheduled jobs within date range
            if (startDate.HasValue || endDate.HasValue)
            {
                query = query.Where(t => t.ServiceJobs.Any(j => 
                    (!startDate.HasValue || j.ScheduledDate >= startDate.Value) &&
                    (!endDate.HasValue || j.ScheduledDate <= endDate.Value)));
            }

            // Apply sorting
            query = sortBy?.ToLowerInvariant() switch
            {
                "name" => sortDescending ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name),
                "status" => sortDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                "location" => sortDescending ? query.OrderByDescending(t => t.BaseLocation) : query.OrderBy(t => t.BaseLocation),
                _ => sortDescending ? query.OrderByDescending(t => t.UpdatedAt) : query.OrderBy(t => t.UpdatedAt)
            };

            var totalCount = await query.CountAsync();
            var technicians = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var technicianDtos = _mapper.Map<List<TechnicianDto>>(technicians);
            
            var pagedResult = new PagedResult<TechnicianDto>
            {
                Items = technicianDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(new ApiResponse<PagedResult<TechnicianDto>>
            {
                Success = true,
                Data = pagedResult,
                Message = $"Retrieved {technicianDtos.Count} technician schedules"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving technician schedules");
            return StatusCode(500, new ApiResponse<PagedResult<TechnicianDto>>
            {
                Success = false,
                Message = "An error occurred while retrieving schedules",
                Errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get detailed schedule for a specific technician
    /// </summary>
    [HttpGet("{technicianId:guid}")]
    public async Task<ActionResult<ApiResponse<TechnicianDto>>> GetTechnicianSchedule(
        Guid technicianId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            var technician = await _context.Technicians
                .Include(t => t.ServiceJobs.Where(j => 
                    (!startDate.HasValue || j.ScheduledDate >= startDate.Value) &&
                    (!endDate.HasValue || j.ScheduledDate <= endDate.Value)))
                .ThenInclude(j => j.Customer)
                .Include(t => t.WorkingHours)
                .FirstOrDefaultAsync(t => t.Id == technicianId);

            if (technician == null)
            {
                return NotFound(new ApiResponse<TechnicianDto>
                {
                    Success = false,
                    Message = "Technician not found"
                });
            }

            var technicianDto = _mapper.Map<TechnicianDto>(technician);

            return Ok(new ApiResponse<TechnicianDto>
            {
                Success = true,
                Data = technicianDto,
                Message = "Technician schedule retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule for technician {TechnicianId}", technicianId);
            return StatusCode(500, new ApiResponse<TechnicianDto>
            {
                Success = false,
                Message = "An error occurred while retrieving the technician schedule",
                Errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get technician availability for a specific date range
    /// </summary>
    [HttpGet("{technicianId:guid}/availability")]
    public async Task<ActionResult<ApiResponse<List<AvailabilitySlotDto>>>> GetTechnicianAvailability(
        Guid technicianId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] int slotDurationMinutes = 60)
    {
        try
        {
            var technician = await _context.Technicians
                .Include(t => t.ServiceJobs.Where(j => 
                    j.ScheduledDate >= startDate && j.ScheduledDate <= endDate &&
                    j.Status != JobStatus.Cancelled))
                .Include(t => t.WorkingHours)
                .FirstOrDefaultAsync(t => t.Id == technicianId);

            if (technician == null)
            {
                return NotFound(new ApiResponse<List<AvailabilitySlotDto>>
                {
                    Success = false,
                    Message = "Technician not found"
                });
            }

            var availabilitySlots = CalculateAvailabilitySlots(technician, startDate, endDate, slotDurationMinutes);

            return Ok(new ApiResponse<List<AvailabilitySlotDto>>
            {
                Success = true,
                Data = availabilitySlots,
                Message = $"Retrieved {availabilitySlots.Count} availability slots"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating availability for technician {TechnicianId}", technicianId);
            return StatusCode(500, new ApiResponse<List<AvailabilitySlotDto>>
            {
                Success = false,
                Message = "An error occurred while calculating availability",
                Errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// Update working hours for a technician
    /// </summary>
    [HttpPut("{technicianId:guid}/working-hours")]
    public async Task<ActionResult<ApiResponse<List<WorkingHoursDto>>>> UpdateWorkingHours(
        Guid technicianId,
        [FromBody] List<WorkingHoursUpdateDto> workingHoursUpdates)
    {
        try
        {
            var technician = await _context.Technicians
                .Include(t => t.WorkingHours)
                .FirstOrDefaultAsync(t => t.Id == technicianId);

            if (technician == null)
            {
                return NotFound(new ApiResponse<List<WorkingHoursDto>>
                {
                    Success = false,
                    Message = "Technician not found"
                });
            }

            // Remove existing working hours
            _context.WorkingHours.RemoveRange(technician.WorkingHours);

            // Add new working hours
            var newWorkingHours = workingHoursUpdates.Select(wh => new WorkingHours
            {
                Id = Guid.NewGuid(),
                TechnicianId = technicianId,
                DayOfWeek = wh.DayOfWeek,
                StartTime = wh.StartTime,
                EndTime = wh.EndTime,
                IsAvailable = wh.IsAvailable,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            await _context.WorkingHours.AddRangeAsync(newWorkingHours);
            
            technician.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var workingHoursDtos = _mapper.Map<List<WorkingHoursDto>>(newWorkingHours);

            return Ok(new ApiResponse<List<WorkingHoursDto>>
            {
                Success = true,
                Data = workingHoursDtos,
                Message = "Working hours updated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating working hours for technician {TechnicianId}", technicianId);
            return StatusCode(500, new ApiResponse<List<WorkingHoursDto>>
            {
                Success = false,
                Message = "An error occurred while updating working hours",
                Errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get schedule conflicts for a technician
    /// </summary>
    [HttpGet("{technicianId:guid}/conflicts")]
    public async Task<ActionResult<ApiResponse<List<ScheduleConflictDto>>>> GetScheduleConflicts(
        Guid technicianId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            var dateStart = startDate ?? DateTime.UtcNow.Date;
            var dateEnd = endDate ?? DateTime.UtcNow.Date.AddDays(30);

            var technician = await _context.Technicians
                .Include(t => t.ServiceJobs.Where(j => 
                    j.ScheduledDate >= dateStart && j.ScheduledDate <= dateEnd &&
                    j.Status != JobStatus.Cancelled))
                .Include(t => t.WorkingHours)
                .FirstOrDefaultAsync(t => t.Id == technicianId);

            if (technician == null)
            {
                return NotFound(new ApiResponse<List<ScheduleConflictDto>>
                {
                    Success = false,
                    Message = "Technician not found"
                });
            }

            var conflicts = DetectScheduleConflicts(technician, dateStart, dateEnd);

            return Ok(new ApiResponse<List<ScheduleConflictDto>>
            {
                Success = true,
                Data = conflicts,
                Message = $"Found {conflicts.Count} schedule conflicts"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting conflicts for technician {TechnicianId}", technicianId);
            return StatusCode(500, new ApiResponse<List<ScheduleConflictDto>>
            {
                Success = false,
                Message = "An error occurred while detecting conflicts",
                Errors = new[] { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get schedule summary for multiple technicians
    /// </summary>
    [HttpPost("summary")]
    public async Task<ActionResult<ApiResponse<List<TechnicianScheduleSummaryDto>>>> GetScheduleSummary(
        [FromBody] ScheduleSummaryRequestDto request)
    {
        try
        {
            var query = _context.Technicians
                .Include(t => t.ServiceJobs.Where(j => 
                    j.ScheduledDate >= request.StartDate && j.ScheduledDate <= request.EndDate))
                .AsQueryable();

            if (request.TechnicianIds?.Any() == true)
                query = query.Where(t => request.TechnicianIds.Contains(t.Id));

            if (request.Status.HasValue)
                query = query.Where(t => t.Status == request.Status.Value);

            var technicians = await query.ToListAsync();

            var summaries = technicians.Select(t => new TechnicianScheduleSummaryDto
            {
                TechnicianId = t.Id,
                TechnicianName = t.Name,
                TotalJobs = t.ServiceJobs.Count,
                CompletedJobs = t.ServiceJobs.Count(j => j.Status == ServiceJobStatus.Completed),
                PendingJobs = t.ServiceJobs.Count(j => j.Status == ServiceJobStatus.Scheduled || j.Status == ServiceJobStatus.InProgress),
                CancelledJobs = t.ServiceJobs.Count(j => j.Status == ServiceJobStatus.Cancelled),
                TotalEstimatedHours = t.ServiceJobs.Sum(j => j.EstimatedDuration?.TotalHours ?? 0),
                UtilizationRate = CalculateUtilizationRate(t, request.StartDate, request.EndDate)
            }).ToList();

            return Ok(new ApiResponse<List<TechnicianScheduleSummaryDto>>
            {
                Success = true,
                Data = summaries,
                Message = $"Retrieved schedule summary for {summaries.Count} technicians"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating schedule summary");
            return StatusCode(500, new ApiResponse<List<TechnicianScheduleSummaryDto>>
            {
                Success = false,
                Message = "An error occurred while generating schedule summary",
                Errors = new[] { ex.Message }
            });
        }
    }

    #region Private Helper Methods

    private List<AvailabilitySlotDto> CalculateAvailabilitySlots(
        Technician technician, 
        DateTime startDate, 
        DateTime endDate, 
        int slotDurationMinutes)
    {
        var slots = new List<AvailabilitySlotDto>();
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            var dayOfWeek = currentDate.DayOfWeek;
            var workingHours = technician.WorkingHours
                .FirstOrDefault(wh => wh.DayOfWeek == dayOfWeek && wh.IsAvailable);

            if (workingHours != null)
            {
                var dayStart = currentDate.Add(workingHours.StartTime);
                var dayEnd = currentDate.Add(workingHours.EndTime);
                var scheduledJobs = technician.ServiceJobs
                    .Where(j => j.ScheduledDate >= dayStart && j.ScheduledDate <= dayEnd)
                    .OrderBy(j => j.ScheduledDate)
                    .ToList();

                var currentTime = dayStart;
                
                foreach (var job in scheduledJobs)
                {
                    // Add available slots before this job
                    while (currentTime.AddMinutes(slotDurationMinutes) <= job.ScheduledDate)
                    {
                        slots.Add(new AvailabilitySlotDto
                        {
                            StartTime = currentTime,
                            EndTime = currentTime.AddMinutes(slotDurationMinutes),
                            IsAvailable = true,
                            TechnicianId = technician.Id
                        });
                        currentTime = currentTime.AddMinutes(slotDurationMinutes);
                    }

                    // Skip the time occupied by this job
                    var jobEndTime = job.ScheduledDate.Add(job.EstimatedDuration ?? TimeSpan.FromHours(1));
                    currentTime = jobEndTime;
                }

                // Add remaining available slots for the day
                while (currentTime.AddMinutes(slotDurationMinutes) <= dayEnd)
                {
                    slots.Add(new AvailabilitySlotDto
                    {
                        StartTime = currentTime,
                        EndTime = currentTime.AddMinutes(slotDurationMinutes),
                        IsAvailable = true,
                        TechnicianId = technician.Id
                    });
                    currentTime = currentTime.AddMinutes(slotDurationMinutes);
                }
            }

            currentDate = currentDate.AddDays(1);
        }

        return slots;
    }

    private List<ScheduleConflictDto> DetectScheduleConflicts(
        Technician technician, 
        DateTime startDate, 
        DateTime endDate)
    {
        var conflicts = new List<ScheduleConflictDto>();
        var jobs = technician.ServiceJobs
            .Where(j => j.ScheduledDate >= startDate && j.ScheduledDate <= endDate)
            .OrderBy(j => j.ScheduledDate)
            .ToList();

        // Check for overlapping jobs
        for (int i = 0; i < jobs.Count - 1; i++)
        {
            var currentJob = jobs[i];
            var nextJob = jobs[i + 1];
            
            var currentJobEnd = currentJob.ScheduledDate.Add(currentJob.EstimatedDuration ?? TimeSpan.FromHours(1));
            
            if (currentJobEnd > nextJob.ScheduledDate)
            {
                conflicts.Add(new ScheduleConflictDto
                {
                    TechnicianId = technician.Id,
                    ConflictType = "Overlapping Jobs",
                    ConflictDate = currentJob.ScheduledDate,
                    Description = $"Job {currentJob.JobNumber} overlaps with job {nextJob.JobNumber}",
                    Job1Id = currentJob.Id,
                    Job2Id = nextJob.Id
                });
            }
        }

        // Check for jobs scheduled outside working hours
        foreach (var job in jobs)
        {
            var jobDate = job.ScheduledDate.Date;
            var dayOfWeek = jobDate.DayOfWeek;
            var workingHours = technician.WorkingHours
                .FirstOrDefault(wh => wh.DayOfWeek == dayOfWeek && wh.IsAvailable);

            if (workingHours == null)
            {
                conflicts.Add(new ScheduleConflictDto
                {
                    TechnicianId = technician.Id,
                    ConflictType = "Outside Working Hours",
                    ConflictDate = job.ScheduledDate,
                    Description = $"Job {job.JobNumber} scheduled on non-working day",
                    Job1Id = job.Id
                });
            }
            else
            {
                var jobTime = job.ScheduledDate.TimeOfDay;
                var jobEndTime = jobTime.Add(job.EstimatedDuration ?? TimeSpan.FromHours(1));

                if (jobTime < workingHours.StartTime || jobEndTime > workingHours.EndTime)
                {
                    conflicts.Add(new ScheduleConflictDto
                    {
                        TechnicianId = technician.Id,
                        ConflictType = "Outside Working Hours",
                        ConflictDate = job.ScheduledDate,
                        Description = $"Job {job.JobNumber} scheduled outside working hours",
                        Job1Id = job.Id
                    });
                }
            }
        }

        return conflicts;
    }

    private double CalculateUtilizationRate(Technician technician, DateTime startDate, DateTime endDate)
    {
        var totalWorkingHours = CalculateTotalWorkingHours(technician, startDate, endDate);
        if (totalWorkingHours == 0) return 0;

        var scheduledHours = technician.ServiceJobs
            .Where(j => j.ScheduledDate >= startDate && j.ScheduledDate <= endDate)
            .Sum(j => j.EstimatedDuration?.TotalHours ?? 0);

        return Math.Min(100, (scheduledHours / totalWorkingHours) * 100);
    }

    private double CalculateTotalWorkingHours(Technician technician, DateTime startDate, DateTime endDate)
    {
        var totalHours = 0.0;
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            var dayOfWeek = currentDate.DayOfWeek;
            var workingHours = technician.WorkingHours
                .FirstOrDefault(wh => wh.DayOfWeek == dayOfWeek && wh.IsAvailable);

            if (workingHours != null)
            {
                totalHours += (workingHours.EndTime - workingHours.StartTime).TotalHours;
            }

            currentDate = currentDate.AddDays(1);
        }

        return totalHours;
    }

    #endregion
}
