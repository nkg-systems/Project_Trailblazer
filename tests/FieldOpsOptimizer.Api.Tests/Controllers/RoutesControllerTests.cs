using FieldOpsOptimizer.Api.Tests.TestBase;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;
using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace FieldOpsOptimizer.Api.Tests.Controllers;

public class RoutesControllerTests : ApiIntegrationTestBase
{
    public RoutesControllerTests(CustomWebApplicationFactory<Program> factory) : base(factory) { }

    [Fact]
    public async Task GetRoutes_ShouldReturnPaginatedRoutesList()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route1 = CreateTestRoute(technician.Id, DateTime.Today);
        var route2 = CreateTestRoute(technician.Id, DateTime.Today.AddDays(1));
        
        Context.Technicians.Add(technician);
        Context.Routes.AddRange(route1, route2);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<PaginatedRoutesResponse>("/api/routes");

        // Assert
        response.Routes.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
        response.PageNumber.Should().Be(1);
        response.PageSize.Should().Be(50);
        response.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GetRoutes_WithTechnicianFilter_ShouldReturnFilteredRoutes()
    {
        // Arrange
        var technician1 = CreateTestTechnician();
        var technician2 = CreateTestTechnician();
        var route1 = CreateTestRoute(technician1.Id, DateTime.Today);
        var route2 = CreateTestRoute(technician2.Id, DateTime.Today);
        
        Context.Technicians.AddRange(technician1, technician2);
        Context.Routes.AddRange(route1, route2);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<PaginatedRoutesResponse>($"/api/routes?technicianId={technician1.Id}");

        // Assert
        response.Routes.Should().HaveCount(1);
        response.Routes.First().TechnicianId.Should().Be(technician1.Id);
    }

    [Fact]
    public async Task GetRoutes_WithDateFilter_ShouldReturnFilteredRoutes()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var todayRoute = CreateTestRoute(technician.Id, DateTime.Today);
        var tomorrowRoute = CreateTestRoute(technician.Id, DateTime.Today.AddDays(1));
        
        Context.Technicians.Add(technician);
        Context.Routes.AddRange(todayRoute, tomorrowRoute);
        await Context.SaveChangesAsync();

        var targetDate = DateTime.Today.ToString("yyyy-MM-dd");

        // Act
        var response = await GetAsync<PaginatedRoutesResponse>($"/api/routes?date={targetDate}");

        // Assert
        response.Routes.Should().HaveCount(1);
        response.Routes.First().Date.Date.Should().Be(DateTime.Today.Date);
    }

    [Fact]
    public async Task GetRoute_WithValidId_ShouldReturnRoute()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        
        Context.Technicians.Add(technician);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<RouteResponse>($"/api/routes/{route.Id}");

        // Assert
        response.Id.Should().Be(route.Id);
        response.TechnicianId.Should().Be(route.TechnicianId);
        response.Date.Should().Be(route.Date);
        response.Status.Should().Be(route.Status.ToString());
    }

    [Fact]
    public async Task GetRoute_WithInvalidId_ShouldReturn404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/routes/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateRoute_WithValidData_ShouldCreateAndReturnRoute()
    {
        // Arrange
        var technician = CreateTestTechnician();
        Context.Technicians.Add(technician);
        await Context.SaveChangesAsync();

        var request = new CreateRouteRequest
        {
            TechnicianId = technician.Id,
            Date = DateTime.Today.AddDays(1),
            StartLocation = new AddressRequest
            {
                Street = Faker.Address.StreetAddress(),
                City = Faker.Address.City(),
                State = Faker.Address.StateAbbr(),
                PostalCode = Faker.Address.ZipCode(),
                Country = "USA",
                Coordinate = new Coordinate(Faker.Address.Latitude(), Faker.Address.Longitude())
            },
            EndLocation = new AddressRequest
            {
                Street = Faker.Address.StreetAddress(),
                City = Faker.Address.City(),
                State = Faker.Address.StateAbbr(),
                PostalCode = Faker.Address.ZipCode(),
                Country = "USA",
                Coordinate = new Coordinate(Faker.Address.Latitude(), Faker.Address.Longitude())
            }
        };

        // Act
        var response = await PostAsync<RouteResponse>("/api/routes", request);

        // Assert
        response.Should().NotBeNull();
        response.TechnicianId.Should().Be(request.TechnicianId);
        response.Date.Should().Be(request.Date);
        response.Status.Should().Be("Planned");
        response.StartLocation.Should().NotBeNull();
        response.EndLocation.Should().NotBeNull();

        // Verify route was saved to database
        var savedRoute = await Context.Routes.FindAsync(response.Id);
        savedRoute.Should().NotBeNull();
        savedRoute!.TechnicianId.Should().Be(request.TechnicianId);
    }

    [Fact]
    public async Task CreateRoute_WithInvalidData_ShouldReturn400()
    {
        // Arrange
        var request = new CreateRouteRequest
        {
            TechnicianId = Guid.Empty, // Invalid: empty GUID
            Date = DateTime.Today.AddDays(-1), // Invalid: past date
            StartLocation = null!, // Invalid: null location
            EndLocation = null!
        };

        // Act
        var response = await Client.PostAsync("/api/routes", 
            new StringContent(JsonSerializer.Serialize(request, JsonOptions), 
            System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OptimizeRoute_ShouldOptimizeStopsOrder()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        
        // Add some jobs to optimize
        var job1 = CreateTestServiceJob();
        var job2 = CreateTestServiceJob(); 
        var job3 = CreateTestServiceJob();

        job1.AssignTechnician(technician.Id);
        job2.AssignTechnician(technician.Id);
        job3.AssignTechnician(technician.Id);
        
        // Add stops to route
        route.AddStop(job1.Id, job1.ServiceAddress, job1.EstimatedDuration, job1.PreferredTimeWindow);
        route.AddStop(job2.Id, job2.ServiceAddress, job2.EstimatedDuration, job2.PreferredTimeWindow);
        route.AddStop(job3.Id, job3.ServiceAddress, job3.EstimatedDuration, job3.PreferredTimeWindow);

        Context.Technicians.Add(technician);
        Context.ServiceJobs.AddRange(job1, job2, job3);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        // Act
        var response = await PostAsync<RouteResponse>($"/api/routes/{route.Id}/optimize", new { });

        // Assert
        response.Id.Should().Be(route.Id);
        response.Status.Should().Be("Optimized");
        response.Stops.Should().HaveCount(3);
        response.TotalDistance.Should().BeGreaterThan(0);
        response.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task AddStopToRoute_ShouldAddStop()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        var job = CreateTestServiceJob();
        job.AssignTechnician(technician.Id);
        
        Context.Technicians.Add(technician);
        Context.ServiceJobs.Add(job);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        var stopRequest = new AddStopRequest
        {
            JobId = job.Id,
            EstimatedDuration = job.EstimatedDuration,
            PreferredTimeWindow = job.PreferredTimeWindow
        };

        // Act
        var response = await PostAsync<RouteResponse>($"/api/routes/{route.Id}/stops", stopRequest);

        // Assert
        response.Stops.Should().HaveCount(1);
        response.Stops.First().JobId.Should().Be(job.Id);
        response.Stops.First().EstimatedDuration.Should().Be(job.EstimatedDuration);

        // Verify stop was persisted
        var updatedRoute = await Context.Routes.FindAsync(route.Id);
        updatedRoute!.Stops.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveStopFromRoute_ShouldRemoveStop()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        var job = CreateTestServiceJob();
        job.AssignTechnician(technician.Id);
        
        // Add stop to route
        route.AddStop(job.Id, job.ServiceAddress, job.EstimatedDuration, job.PreferredTimeWindow);
        
        Context.Technicians.Add(technician);
        Context.ServiceJobs.Add(job);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        var stopId = route.Stops.First().Id;

        // Act
        await DeleteAsync($"/api/routes/{route.Id}/stops/{stopId}");

        // Assert
        var updatedRoute = await Context.Routes.FindAsync(route.Id);
        updatedRoute!.Stops.Should().BeEmpty();
    }

    [Fact]
    public async Task ReorderStops_ShouldUpdateStopsOrder()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        
        var job1 = CreateTestServiceJob();
        var job2 = CreateTestServiceJob();
        var job3 = CreateTestServiceJob();
        
        job1.AssignTechnician(technician.Id);
        job2.AssignTechnician(technician.Id);
        job3.AssignTechnician(technician.Id);

        route.AddStop(job1.Id, job1.ServiceAddress, job1.EstimatedDuration, job1.PreferredTimeWindow);
        route.AddStop(job2.Id, job2.ServiceAddress, job2.EstimatedDuration, job2.PreferredTimeWindow);
        route.AddStop(job3.Id, job3.ServiceAddress, job3.EstimatedDuration, job3.PreferredTimeWindow);

        Context.Technicians.Add(technician);
        Context.ServiceJobs.AddRange(job1, job2, job3);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        var stops = route.Stops.ToList();
        var reorderRequest = new ReorderStopsRequest
        {
            StopIds = new List<Guid> { stops[2].Id, stops[0].Id, stops[1].Id } // Reverse order
        };

        // Act
        var response = await PostAsync<RouteResponse>($"/api/routes/{route.Id}/reorder", reorderRequest);

        // Assert
        response.Stops.Should().HaveCount(3);
        response.Stops.First().Id.Should().Be(stops[2].Id);
        response.Stops.Last().Id.Should().Be(stops[1].Id);
    }

    [Fact]
    public async Task StartRoute_ShouldUpdateRouteStatus()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        
        Context.Technicians.Add(technician);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        // Act
        var response = await PostAsync<RouteResponse>($"/api/routes/{route.Id}/start", new { });

        // Assert
        response.Status.Should().Be("InProgress");
        response.StartedAt.Should().NotBeNull();

        // Verify status was persisted
        var updatedRoute = await Context.Routes.FindAsync(route.Id);
        updatedRoute!.Status.Should().Be(RouteStatus.InProgress);
        updatedRoute.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteRoute_ShouldUpdateRouteStatus()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        route.Start(); // Route must be started before completion
        
        Context.Technicians.Add(technician);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        // Act
        var response = await PostAsync<RouteResponse>($"/api/routes/{route.Id}/complete", new { });

        // Assert
        response.Status.Should().Be("Completed");
        response.CompletedAt.Should().NotBeNull();

        // Verify status was persisted
        var updatedRoute = await Context.Routes.FindAsync(route.Id);
        updatedRoute!.Status.Should().Be(RouteStatus.Completed);
        updatedRoute.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRouteProgress_ShouldReturnProgress()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        
        var job1 = CreateTestServiceJob();
        var job2 = CreateTestServiceJob();
        
        job1.AssignTechnician(technician.Id);
        job2.AssignTechnician(technician.Id);
        job1.UpdateStatus(JobStatus.Completed); // One job completed

        route.AddStop(job1.Id, job1.ServiceAddress, job1.EstimatedDuration, job1.PreferredTimeWindow);
        route.AddStop(job2.Id, job2.ServiceAddress, job2.EstimatedDuration, job2.PreferredTimeWindow);
        route.Start();

        Context.Technicians.Add(technician);
        Context.ServiceJobs.AddRange(job1, job2);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<RouteProgressResponse>($"/api/routes/{route.Id}/progress");

        // Assert
        response.RouteId.Should().Be(route.Id);
        response.TotalStops.Should().Be(2);
        response.CompletedStops.Should().Be(1);
        response.ProgressPercentage.Should().Be(50.0);
    }

    [Fact]
    public async Task GetRouteMetrics_ShouldReturnMetrics()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        route.Start();
        route.Complete();
        
        Context.Technicians.Add(technician);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<RouteMetricsResponse>($"/api/routes/{route.Id}/metrics");

        // Assert
        response.RouteId.Should().Be(route.Id);
        response.TotalDistance.Should().BeGreaterOrEqualTo(0);
        response.TotalDuration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        response.ActualDuration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task DeleteRoute_ShouldRemoveRoute()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        
        Context.Technicians.Add(technician);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        // Act
        await DeleteAsync($"/api/routes/{route.Id}");

        // Assert
        var deletedRoute = await Context.Routes.FindAsync(route.Id);
        deletedRoute.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRoute_InProgress_ShouldReturn400()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var route = CreateTestRoute(technician.Id, DateTime.Today);
        route.Start(); // Route is in progress
        
        Context.Technicians.Add(technician);
        Context.Routes.Add(route);
        await Context.SaveChangesAsync();

        // Act
        var response = await Client.DeleteAsync($"/api/routes/{route.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

// DTOs for testing - these should match your actual DTOs from the API project
public record PaginatedRoutesResponse
{
    public List<RouteResponse> Routes { get; init; } = new();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record RouteResponse
{
    public Guid Id { get; init; }
    public Guid TechnicianId { get; init; }
    public DateTime Date { get; init; }
    public string Status { get; init; } = string.Empty;
    public AddressResponse StartLocation { get; init; } = new();
    public AddressResponse EndLocation { get; init; } = new();
    public List<RouteStopResponse> Stops { get; init; } = new();
    public double TotalDistance { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record RouteStopResponse
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public int Order { get; init; }
    public AddressResponse Address { get; init; } = new();
    public TimeSpan EstimatedDuration { get; init; }
    public TimeSpan? PreferredTimeWindow { get; init; }
    public DateTime? EstimatedArrival { get; init; }
    public DateTime? ActualArrival { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string Status { get; init; } = string.Empty;
}

public record CreateRouteRequest
{
    public required Guid TechnicianId { get; init; }
    public required DateTime Date { get; init; }
    public required AddressRequest StartLocation { get; init; }
    public required AddressRequest EndLocation { get; init; }
}

public record AddStopRequest
{
    public required Guid JobId { get; init; }
    public required TimeSpan EstimatedDuration { get; init; }
    public TimeSpan? PreferredTimeWindow { get; init; }
}

public record ReorderStopsRequest
{
    public required List<Guid> StopIds { get; init; }
}

public record RouteProgressResponse
{
    public Guid RouteId { get; init; }
    public int TotalStops { get; init; }
    public int CompletedStops { get; init; }
    public double ProgressPercentage { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public TimeSpan EstimatedTimeRemaining { get; init; }
    public RouteStopResponse? CurrentStop { get; init; }
}

public record RouteMetricsResponse
{
    public Guid RouteId { get; init; }
    public double TotalDistance { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public TimeSpan ActualDuration { get; init; }
    public int TotalStops { get; init; }
    public double AverageStopDuration { get; init; }
    public double EfficiencyScore { get; init; }
}
