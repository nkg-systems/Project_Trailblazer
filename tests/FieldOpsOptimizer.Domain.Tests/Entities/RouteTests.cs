using AutoFixture.Xunit2;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Tests.TestBase;
using FieldOpsOptimizer.Domain.ValueObjects;
using FluentAssertions;

namespace FieldOpsOptimizer.Domain.Tests.Entities;

public class RouteTests : DomainTestBase
{
    [Fact]
    public void Route_Constructor_ShouldInitializeWithValidData()
    {
        // Arrange
        var name = "Morning Route";
        var scheduledDate = DateTime.Today.AddDays(1);
        var assignedTechnicianId = Guid.NewGuid();
        var tenantId = "test-tenant";
        var optimizationObjective = OptimizationObjective.MinimizeTime;

        // Act
        var route = new Route(name, scheduledDate, assignedTechnicianId, tenantId, optimizationObjective);

        // Assert
        route.Name.Should().Be(name);
        route.ScheduledDate.Should().Be(scheduledDate);
        route.AssignedTechnicianId.Should().Be(assignedTechnicianId);
        route.TenantId.Should().Be(tenantId);
        route.OptimizationObjective.Should().Be(optimizationObjective);
        route.Status.Should().Be(RouteStatus.Draft);
        route.TotalDistanceKm.Should().Be(0);
        route.EstimatedDuration.Should().Be(TimeSpan.Zero);
        route.StartedAt.Should().BeNull();
        route.CompletedAt.Should().BeNull();
        route.Stops.Should().BeEmpty();
    }

    [Fact]
    public void Route_Constructor_WithDefaultOptimizationObjective_ShouldInitializeCorrectly()
    {
        // Arrange
        var name = "Morning Route";
        var scheduledDate = DateTime.Today.AddDays(1);
        var assignedTechnicianId = Guid.NewGuid();
        var tenantId = "test-tenant";

        // Act
        var route = new Route(name, scheduledDate, assignedTechnicianId, tenantId);

        // Assert
        route.OptimizationObjective.Should().Be(OptimizationObjective.MinimizeDistance);
    }

    [Fact]
    public void AddStop_ShouldAddStopToRoute()
    {
        // Arrange
        var route = CreateValidRoute();
        var job = CreateValidServiceJob();
        var sequenceOrder = 1;
        var estimatedTravelTime = TimeSpan.FromMinutes(15);
        var originalUpdatedAt = route.UpdatedAt;

        // Act
        route.AddStop(job, sequenceOrder, estimatedTravelTime);

        // Assert
        route.Stops.Should().HaveCount(1);
        var stop = route.Stops.First();
        stop.JobId.Should().Be(job.Id);
        stop.RouteId.Should().Be(route.Id);
        stop.SequenceOrder.Should().Be(sequenceOrder);
        stop.EstimatedTravelTime.Should().Be(estimatedTravelTime);
        route.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void AddStop_MultipleStops_ShouldMaintainOrder()
    {
        // Arrange
        var route = CreateValidRoute();
        var job1 = CreateValidServiceJob();
        var job2 = CreateValidServiceJob();
        var job3 = CreateValidServiceJob();

        // Act
        route.AddStop(job1, 2, TimeSpan.FromMinutes(10));
        route.AddStop(job2, 1, TimeSpan.FromMinutes(15));
        route.AddStop(job3, 3, TimeSpan.FromMinutes(20));

        // Assert
        route.Stops.Should().HaveCount(3);
        var orderedStops = route.Stops.ToList();
        orderedStops[0].JobId.Should().Be(job2.Id); // Sequence 1
        orderedStops[1].JobId.Should().Be(job1.Id); // Sequence 2
        orderedStops[2].JobId.Should().Be(job3.Id); // Sequence 3
    }

    [Fact]
    public void RemoveStop_ShouldRemoveStopAndResequence()
    {
        // Arrange
        var route = CreateValidRoute();
        var job1 = CreateValidServiceJob();
        var job2 = CreateValidServiceJob();
        var job3 = CreateValidServiceJob();
        
        route.AddStop(job1, 1, TimeSpan.FromMinutes(10));
        route.AddStop(job2, 2, TimeSpan.FromMinutes(15));
        route.AddStop(job3, 3, TimeSpan.FromMinutes(20));

        // Act
        route.RemoveStop(job2.Id);

        // Assert
        route.Stops.Should().HaveCount(2);
        var orderedStops = route.Stops.ToList();
        orderedStops[0].JobId.Should().Be(job1.Id);
        orderedStops[0].SequenceOrder.Should().Be(1);
        orderedStops[1].JobId.Should().Be(job3.Id);
        orderedStops[1].SequenceOrder.Should().Be(2); // Should be resequenced from 3 to 2
    }

    [Fact]
    public void RemoveStop_NonExistentStop_ShouldNotThrow()
    {
        // Arrange
        var route = CreateValidRoute();
        var nonExistentJobId = Guid.NewGuid();

        // Act & Assert
        var act = () => route.RemoveStop(nonExistentJobId);
        act.Should().NotThrow();
    }

    [Fact]
    public void OptimizeStops_ShouldReplaceStopsAndUpdateStatus()
    {
        // Arrange
        var route = CreateValidRoute();
        var job1 = CreateValidServiceJob();
        var job2 = CreateValidServiceJob();
        
        route.AddStop(job1, 1, TimeSpan.FromMinutes(10));
        route.AddStop(job2, 2, TimeSpan.FromMinutes(15));

        var optimizedStops = new List<RouteStop>
        {
            new(job2.Id, route.Id, 1, TimeSpan.FromMinutes(12), 5.5),
            new(job1.Id, route.Id, 2, TimeSpan.FromMinutes(8), 3.2)
        };

        // Act
        route.OptimizeStops(optimizedStops);

        // Assert
        route.Status.Should().Be(RouteStatus.Optimized);
        route.Stops.Should().HaveCount(2);
        route.Stops.First().JobId.Should().Be(job2.Id); // job2 is now first
        route.TotalDistanceKm.Should().Be(8.7); // 5.5 + 3.2
        route.EstimatedDuration.Should().Be(TimeSpan.FromMinutes(20)); // 12 + 8
    }

    [Theory]
    [InlineData(RouteStatus.InProgress)]
    [InlineData(RouteStatus.Completed)]
    [InlineData(RouteStatus.Optimized)]
    public void UpdateStatus_ShouldChangeStatus(RouteStatus newStatus)
    {
        // Arrange
        var route = CreateValidRoute();
        var originalUpdatedAt = route.UpdatedAt;

        // Act
        route.UpdateStatus(newStatus);

        // Assert
        route.Status.Should().Be(newStatus);
        route.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdateStatus_ToInProgress_ShouldSetStartedAt()
    {
        // Arrange
        var route = CreateValidRoute();
        var beforeStart = DateTime.UtcNow;

        // Act
        route.UpdateStatus(RouteStatus.InProgress);

        // Assert
        route.Status.Should().Be(RouteStatus.InProgress);
        route.StartedAt.Should().NotBeNull();
        route.StartedAt.Should().BeOnOrAfter(beforeStart);
    }

    [Fact]
    public void UpdateStatus_ToInProgress_WhenAlreadyStarted_ShouldNotUpdateStartedAt()
    {
        // Arrange
        var route = CreateValidRoute();
        route.UpdateStatus(RouteStatus.InProgress);
        var originalStartedAt = route.StartedAt;

        // Act
        route.UpdateStatus(RouteStatus.Draft); // Change to another status
        route.UpdateStatus(RouteStatus.InProgress); // Back to InProgress

        // Assert
        route.StartedAt.Should().Be(originalStartedAt);
    }

    [Fact]
    public void UpdateStatus_ToCompleted_ShouldSetCompletedAt()
    {
        // Arrange
        var route = CreateValidRoute();
        var beforeCompletion = DateTime.UtcNow;

        // Act
        route.UpdateStatus(RouteStatus.Completed);

        // Assert
        route.Status.Should().Be(RouteStatus.Completed);
        route.CompletedAt.Should().NotBeNull();
        route.CompletedAt.Should().BeOnOrAfter(beforeCompletion);
    }

    [Fact]
    public void UpdateStatus_ToCompleted_WhenAlreadyCompleted_ShouldNotUpdateCompletedAt()
    {
        // Arrange
        var route = CreateValidRoute();
        route.UpdateStatus(RouteStatus.Completed);
        var originalCompletedAt = route.CompletedAt;

        // Act
        route.UpdateStatus(RouteStatus.Draft); // Change to another status
        route.UpdateStatus(RouteStatus.Completed); // Back to Completed

        // Assert
        route.CompletedAt.Should().Be(originalCompletedAt);
    }

    [Fact]
    public void MarkStopCompleted_ShouldCompleteStop()
    {
        // Arrange
        var route = CreateValidRoute();
        var job = CreateValidServiceJob();
        var completedAt = DateTime.UtcNow;
        
        route.AddStop(job, 1, TimeSpan.FromMinutes(15));

        // Act
        route.MarkStopCompleted(job.Id, completedAt);

        // Assert
        var stop = route.Stops.First();
        stop.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void MarkStopCompleted_WhenAllStopsCompleted_ShouldCompleteRoute()
    {
        // Arrange
        var route = CreateValidRoute();
        var job1 = CreateValidServiceJob();
        var job2 = CreateValidServiceJob();
        var completedAt = DateTime.UtcNow;
        
        route.AddStop(job1, 1, TimeSpan.FromMinutes(15));
        route.AddStop(job2, 2, TimeSpan.FromMinutes(20));

        // Act
        route.MarkStopCompleted(job1.Id, completedAt);
        route.Status.Should().Be(RouteStatus.Draft); // Should not be completed yet
        
        route.MarkStopCompleted(job2.Id, completedAt);

        // Assert
        route.Status.Should().Be(RouteStatus.Completed);
        route.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkStopCompleted_NonExistentStop_ShouldNotThrow()
    {
        // Arrange
        var route = CreateValidRoute();
        var nonExistentJobId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;

        // Act & Assert
        var act = () => route.MarkStopCompleted(nonExistentJobId, completedAt);
        act.Should().NotThrow();
    }

    [Fact]
    public void ReassignTechnician_ShouldUpdateTechnicianId()
    {
        // Arrange
        var route = CreateValidRoute();
        var newTechnicianId = Guid.NewGuid();
        var originalUpdatedAt = route.UpdatedAt;

        // Act
        route.ReassignTechnician(newTechnicianId);

        // Assert
        route.AssignedTechnicianId.Should().Be(newTechnicianId);
        route.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void GetNextStop_ShouldReturnFirstIncompleteStop()
    {
        // Arrange
        var route = CreateValidRoute();
        var job1 = CreateValidServiceJob();
        var job2 = CreateValidServiceJob();
        var job3 = CreateValidServiceJob();
        
        route.AddStop(job1, 1, TimeSpan.FromMinutes(10));
        route.AddStop(job2, 2, TimeSpan.FromMinutes(15));
        route.AddStop(job3, 3, TimeSpan.FromMinutes(20));

        // Mark first stop as completed
        route.MarkStopCompleted(job1.Id, DateTime.UtcNow);

        // Act
        var nextStop = route.GetNextStop();

        // Assert
        nextStop.Should().NotBeNull();
        nextStop!.JobId.Should().Be(job2.Id);
        nextStop.SequenceOrder.Should().Be(2);
    }

    [Fact]
    public void GetNextStop_WhenAllCompleted_ShouldReturnNull()
    {
        // Arrange
        var route = CreateValidRoute();
        var job1 = CreateValidServiceJob();
        var job2 = CreateValidServiceJob();
        
        route.AddStop(job1, 1, TimeSpan.FromMinutes(10));
        route.AddStop(job2, 2, TimeSpan.FromMinutes(15));

        // Mark all stops as completed
        route.MarkStopCompleted(job1.Id, DateTime.UtcNow);
        route.MarkStopCompleted(job2.Id, DateTime.UtcNow);

        // Act
        var nextStop = route.GetNextStop();

        // Assert
        nextStop.Should().BeNull();
    }

    [Fact]
    public void GetNextStop_WhenNoStops_ShouldReturnNull()
    {
        // Arrange
        var route = CreateValidRoute();

        // Act
        var nextStop = route.GetNextStop();

        // Assert
        nextStop.Should().BeNull();
    }

    [Fact]
    public void GetCurrentStop_ShouldReturnStartedIncompleteStop()
    {
        // Arrange
        var route = CreateValidRoute();
        var job1 = CreateValidServiceJob();
        var job2 = CreateValidServiceJob();
        
        route.AddStop(job1, 1, TimeSpan.FromMinutes(10));
        route.AddStop(job2, 2, TimeSpan.FromMinutes(15));

        // Mark first stop as started but not completed
        var stops = route.Stops.ToList();
        stops[0].MarkStarted(DateTime.UtcNow);

        // Act
        var currentStop = route.GetCurrentStop();

        // Assert
        currentStop.Should().NotBeNull();
        currentStop!.JobId.Should().Be(job1.Id);
    }

    [Fact]
    public void GetCurrentStop_WhenNoStartedStops_ShouldReturnNull()
    {
        // Arrange
        var route = CreateValidRoute();
        var job = CreateValidServiceJob();
        route.AddStop(job, 1, TimeSpan.FromMinutes(15));

        // Act
        var currentStop = route.GetCurrentStop();

        // Assert
        currentStop.Should().BeNull();
    }

    [Theory]
    [DomainAutoData]
    public void Route_PropertyChanges_ShouldUpdateTimestamp(
        string name,
        DateTime scheduledDate,
        Guid assignedTechnicianId,
        string tenantId)
    {
        // Arrange
        var route = new Route(name, scheduledDate, assignedTechnicianId, tenantId);
        var originalTimestamp = route.UpdatedAt;

        // Act & Assert - Test multiple operations update the timestamp
        route.ReassignTechnician(Guid.NewGuid());
        route.UpdatedAt.Should().BeAfter(originalTimestamp);

        var timestamp2 = route.UpdatedAt;
        route.UpdateStatus(RouteStatus.InProgress);
        route.UpdatedAt.Should().BeAfter(timestamp2);

        var timestamp3 = route.UpdatedAt;
        var job = CreateValidServiceJob();
        route.AddStop(job, 1, TimeSpan.FromMinutes(15));
        route.UpdatedAt.Should().BeAfter(timestamp3);
    }
}

public class RouteStopTests : DomainTestBase
{
    [Fact]
    public void RouteStop_Constructor_ShouldInitializeWithValidData()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var sequenceOrder = 2;
        var estimatedTravelTime = TimeSpan.FromMinutes(25);
        var distanceFromPrevious = 5.7;

        // Act
        var routeStop = new RouteStop(jobId, routeId, sequenceOrder, estimatedTravelTime, distanceFromPrevious);

        // Assert
        routeStop.JobId.Should().Be(jobId);
        routeStop.RouteId.Should().Be(routeId);
        routeStop.SequenceOrder.Should().Be(sequenceOrder);
        routeStop.EstimatedTravelTime.Should().Be(estimatedTravelTime);
        routeStop.DistanceFromPreviousKm.Should().Be(distanceFromPrevious);
        routeStop.EstimatedArrival.Should().BeNull();
        routeStop.StartedAt.Should().BeNull();
        routeStop.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void RouteStop_Constructor_WithDefaultDistance_ShouldInitializeCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var sequenceOrder = 1;
        var estimatedTravelTime = TimeSpan.FromMinutes(15);

        // Act
        var routeStop = new RouteStop(jobId, routeId, sequenceOrder, estimatedTravelTime);

        // Assert
        routeStop.DistanceFromPreviousKm.Should().Be(0);
    }

    [Fact]
    public void UpdateSequenceOrder_ShouldChangeSequenceOrder()
    {
        // Arrange
        var routeStop = new RouteStop(Guid.NewGuid(), Guid.NewGuid(), 1, TimeSpan.FromMinutes(15));
        var newOrder = 3;

        // Act
        routeStop.UpdateSequenceOrder(newOrder);

        // Assert
        routeStop.SequenceOrder.Should().Be(newOrder);
    }

    [Fact]
    public void UpdateTravelInfo_ShouldUpdateTravelTimeAndDistance()
    {
        // Arrange
        var routeStop = new RouteStop(Guid.NewGuid(), Guid.NewGuid(), 1, TimeSpan.FromMinutes(15), 2.5);
        var newTravelTime = TimeSpan.FromMinutes(20);
        var newDistance = 3.8;

        // Act
        routeStop.UpdateTravelInfo(newTravelTime, newDistance);

        // Assert
        routeStop.EstimatedTravelTime.Should().Be(newTravelTime);
        routeStop.DistanceFromPreviousKm.Should().Be(newDistance);
    }

    [Fact]
    public void SetEstimatedArrival_ShouldSetArrivalTime()
    {
        // Arrange
        var routeStop = new RouteStop(Guid.NewGuid(), Guid.NewGuid(), 1, TimeSpan.FromMinutes(15));
        var estimatedArrival = DateTime.Today.AddHours(10).AddMinutes(30);

        // Act
        routeStop.SetEstimatedArrival(estimatedArrival);

        // Assert
        routeStop.EstimatedArrival.Should().Be(estimatedArrival);
    }

    [Fact]
    public void MarkStarted_ShouldSetStartedAt()
    {
        // Arrange
        var routeStop = new RouteStop(Guid.NewGuid(), Guid.NewGuid(), 1, TimeSpan.FromMinutes(15));
        var startedAt = DateTime.UtcNow;

        // Act
        routeStop.MarkStarted(startedAt);

        // Assert
        routeStop.StartedAt.Should().Be(startedAt);
    }

    [Fact]
    public void MarkCompleted_ShouldSetCompletedAt()
    {
        // Arrange
        var routeStop = new RouteStop(Guid.NewGuid(), Guid.NewGuid(), 1, TimeSpan.FromMinutes(15));
        var completedAt = DateTime.UtcNow;
        var startedAt = completedAt.AddMinutes(-30);
        routeStop.MarkStarted(startedAt);

        // Act
        routeStop.MarkCompleted(completedAt);

        // Assert
        routeStop.CompletedAt.Should().Be(completedAt);
        routeStop.StartedAt.Should().Be(startedAt);
    }

    [Fact]
    public void MarkCompleted_WithoutStarted_ShouldSetBothStartedAndCompleted()
    {
        // Arrange
        var routeStop = new RouteStop(Guid.NewGuid(), Guid.NewGuid(), 1, TimeSpan.FromMinutes(15));
        var completedAt = DateTime.UtcNow;

        // Act
        routeStop.MarkCompleted(completedAt);

        // Assert
        routeStop.CompletedAt.Should().Be(completedAt);
        routeStop.StartedAt.Should().Be(completedAt);
    }
}
