using FieldOpsOptimizer.Domain.Common;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Domain.Entities;

public class Route : BaseEntity
{
    private readonly List<RouteStop> _stops = new();

    public string Name { get; private set; } = string.Empty;
    public DateTime ScheduledDate { get; private set; }
    public Guid AssignedTechnicianId { get; private set; }
    public RouteStatus Status { get; private set; } = RouteStatus.Draft;
    public double TotalDistanceKm { get; private set; }
    public TimeSpan EstimatedDuration { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public OptimizationObjective OptimizationObjective { get; private set; } = OptimizationObjective.MinimizeDistance;

    public IReadOnlyList<RouteStop> Stops => _stops.OrderBy(s => s.SequenceOrder).ToList().AsReadOnly();

    // Navigation properties
    public Technician AssignedTechnician { get; private set; } = null!;

    private Route() { } // For EF Core

    public Route(
        string name,
        DateTime scheduledDate,
        Guid assignedTechnicianId,
        string tenantId,
        OptimizationObjective optimizationObjective = OptimizationObjective.MinimizeDistance)
    {
        Name = name;
        ScheduledDate = scheduledDate;
        AssignedTechnicianId = assignedTechnicianId;
        TenantId = tenantId;
        OptimizationObjective = optimizationObjective;
    }

    public void AddStop(ServiceJob job, int sequenceOrder, TimeSpan estimatedTravelTime)
    {
        var stop = new RouteStop(job.Id, Id, sequenceOrder, estimatedTravelTime);
        _stops.Add(stop);
        UpdateTimestamp();
    }

    public void RemoveStop(Guid jobId)
    {
        _stops.RemoveAll(s => s.JobId == jobId);
        ResequenceStops();
        UpdateTimestamp();
    }

    public void OptimizeStops(List<RouteStop> optimizedStops)
    {
        _stops.Clear();
        _stops.AddRange(optimizedStops);
        Status = RouteStatus.Optimized;
        CalculateTotals();
        UpdateTimestamp();
    }

    public void UpdateStatus(RouteStatus status)
    {
        Status = status;
        
        switch (status)
        {
            case RouteStatus.InProgress when StartedAt == null:
                StartedAt = DateTime.UtcNow;
                break;
            case RouteStatus.Completed when CompletedAt == null:
                CompletedAt = DateTime.UtcNow;
                break;
        }
        
        UpdateTimestamp();
    }

    public void MarkStopCompleted(Guid jobId, DateTime completedAt)
    {
        var stop = _stops.FirstOrDefault(s => s.JobId == jobId);
        if (stop != null)
        {
            stop.MarkCompleted(completedAt);
            
            // Check if all stops are completed
            if (_stops.All(s => s.CompletedAt.HasValue))
            {
                UpdateStatus(RouteStatus.Completed);
            }
            
            UpdateTimestamp();
        }
    }

    public void ReassignTechnician(Guid newTechnicianId)
    {
        AssignedTechnicianId = newTechnicianId;
        UpdateTimestamp();
    }

    private void ResequenceStops()
    {
        var orderedStops = _stops.OrderBy(s => s.SequenceOrder).ToList();
        for (int i = 0; i < orderedStops.Count; i++)
        {
            orderedStops[i].UpdateSequenceOrder(i + 1);
        }
    }

    private void CalculateTotals()
    {
        TotalDistanceKm = _stops.Sum(s => s.DistanceFromPreviousKm);
        EstimatedDuration = TimeSpan.FromTicks(_stops.Sum(s => s.EstimatedTravelTime.Ticks));
    }

    public RouteStop? GetNextStop()
    {
        return _stops
            .Where(s => !s.CompletedAt.HasValue)
            .OrderBy(s => s.SequenceOrder)
            .FirstOrDefault();
    }

    public RouteStop? GetCurrentStop()
    {
        return _stops
            .Where(s => s.StartedAt.HasValue && !s.CompletedAt.HasValue)
            .FirstOrDefault();
    }
}

public class RouteStop
{
    public Guid JobId { get; private set; }
    public Guid RouteId { get; private set; }
    public int SequenceOrder { get; private set; }
    public TimeSpan EstimatedTravelTime { get; private set; }
    public double DistanceFromPreviousKm { get; private set; }
    public DateTime? EstimatedArrival { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Navigation properties
    public ServiceJob Job { get; private set; } = null!;
    public Route Route { get; private set; } = null!;

    private RouteStop() { } // For EF Core

    public RouteStop(Guid jobId, Guid routeId, int sequenceOrder, TimeSpan estimatedTravelTime, double distanceFromPreviousKm = 0)
    {
        JobId = jobId;
        RouteId = routeId;
        SequenceOrder = sequenceOrder;
        EstimatedTravelTime = estimatedTravelTime;
        DistanceFromPreviousKm = distanceFromPreviousKm;
    }

    public void UpdateSequenceOrder(int newOrder)
    {
        SequenceOrder = newOrder;
    }

    public void UpdateTravelInfo(TimeSpan travelTime, double distance)
    {
        EstimatedTravelTime = travelTime;
        DistanceFromPreviousKm = distance;
    }

    public void SetEstimatedArrival(DateTime estimatedArrival)
    {
        EstimatedArrival = estimatedArrival;
    }

    public void MarkStarted(DateTime startedAt)
    {
        StartedAt = startedAt;
    }

    public void MarkCompleted(DateTime completedAt)
    {
        CompletedAt = completedAt;
        if (!StartedAt.HasValue)
        {
            StartedAt = completedAt; // Assume started and completed at the same time if not tracked
        }
    }
}
