using FieldOpsOptimizer.Api.Tests.TestBase;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;
using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace FieldOpsOptimizer.Api.Tests.Controllers;

public class TechniciansControllerTests : ApiIntegrationTestBase
{
    public TechniciansControllerTests(CustomWebApplicationFactory<Program> factory) : base(factory) { }

    [Fact]
    public async Task GetTechnicians_ShouldReturnPaginatedTechniciansList()
    {
        // Arrange
        var technician1 = CreateTestTechnician();
        var technician2 = CreateTestTechnician();
        
        Context.Technicians.AddRange(technician1, technician2);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<PaginatedTechniciansResponse>("/api/technicians");

        // Assert
        response.Technicians.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
        response.PageNumber.Should().Be(1);
        response.PageSize.Should().Be(50);
        response.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GetTechnicians_WithSkillsFilter_ShouldReturnFilteredTechnicians()
    {
        // Arrange
        var technician1 = CreateTestTechnician(); // Has Electrical, Plumbing
        var technician2 = CreateTestTechnician();
        technician2.AddSkill("HVAC", 5);
        
        Context.Technicians.AddRange(technician1, technician2);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<PaginatedTechniciansResponse>("/api/technicians?skills=HVAC");

        // Assert
        response.Technicians.Should().HaveCount(1);
        response.Technicians.First().Skills.Should().ContainKey("HVAC");
    }

    [Fact]
    public async Task GetTechnicians_WithStatusFilter_ShouldReturnFilteredTechnicians()
    {
        // Arrange
        var technician1 = CreateTestTechnician(); // Active by default
        var technician2 = CreateTestTechnician();
        technician2.UpdateStatus(TechnicianStatus.Unavailable);
        
        Context.Technicians.AddRange(technician1, technician2);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<PaginatedTechniciansResponse>("/api/technicians?status=Available");

        // Assert
        response.Technicians.Should().HaveCount(1);
        response.Technicians.First().Status.Should().Be("Available");
    }

    [Fact]
    public async Task GetTechnician_WithValidId_ShouldReturnTechnician()
    {
        // Arrange
        var technician = CreateTestTechnician();
        Context.Technicians.Add(technician);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<TechnicianResponse>($"/api/technicians/{technician.Id}");

        // Assert
        response.Id.Should().Be(technician.Id);
        response.FirstName.Should().Be(technician.FirstName);
        response.LastName.Should().Be(technician.LastName);
        response.Email.Should().Be(technician.Email);
    }

    [Fact]
    public async Task GetTechnician_WithInvalidId_ShouldReturn404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/technicians/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTechnician_WithValidData_ShouldCreateAndReturnTechnician()
    {
        // Arrange
        var request = new CreateTechnicianRequest
        {
            FirstName = Faker.Name.FirstName(),
            LastName = Faker.Name.LastName(),
            Email = Faker.Internet.Email(),
            PhoneNumber = Faker.Phone.PhoneNumber(),
            Address = new AddressRequest
            {
                Street = Faker.Address.StreetAddress(),
                City = Faker.Address.City(),
                State = Faker.Address.StateAbbr(),
                PostalCode = Faker.Address.ZipCode(),
                Country = "USA",
                Coordinate = new Coordinate(Faker.Address.Latitude(), Faker.Address.Longitude())
            },
            Skills = new Dictionary<string, int>
            {
                { "Electrical", 5 },
                { "Plumbing", 3 }
            },
            HourlyRate = 75.00m,
            StartDate = DateTime.Today
        };

        // Act
        var response = await PostAsync<TechnicianResponse>("/api/technicians", request);

        // Assert
        response.Should().NotBeNull();
        response.FirstName.Should().Be(request.FirstName);
        response.LastName.Should().Be(request.LastName);
        response.Email.Should().Be(request.Email);
        response.Status.Should().Be("Available");
        response.Skills.Should().BeEquivalentTo(request.Skills);
        response.EmployeeId.Should().StartWith("TECH");

        // Verify technician was saved to database
        var savedTechnician = await Context.Technicians.FindAsync(response.Id);
        savedTechnician.Should().NotBeNull();
        savedTechnician!.Email.Should().Be(request.Email);
    }

    [Fact]
    public async Task CreateTechnician_WithInvalidData_ShouldReturn400()
    {
        // Arrange
        var request = new CreateTechnicianRequest
        {
            FirstName = "", // Invalid: empty name
            LastName = "",
            Email = "invalid-email", // Invalid: bad email format
            PhoneNumber = "",
            Address = null!, // Invalid: null address
            StartDate = DateTime.Today.AddDays(1) // Invalid: future start date
        };

        // Act
        var response = await Client.PostAsync("/api/technicians", 
            new StringContent(JsonSerializer.Serialize(request, JsonOptions), 
            System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTechnician_WithValidData_ShouldUpdateTechnician()
    {
        // Arrange
        var technician = CreateTestTechnician();
        Context.Technicians.Add(technician);
        await Context.SaveChangesAsync();

        var updateRequest = new UpdateTechnicianRequest
        {
            FirstName = "Updated First Name",
            LastName = "Updated Last Name",
            PhoneNumber = "555-0199",
            HourlyRate = 85.00m,
            Status = TechnicianStatus.Unavailable
        };

        // Act
        var response = await PutAsync<TechnicianResponse>($"/api/technicians/{technician.Id}", updateRequest);

        // Assert
        response.FirstName.Should().Be(updateRequest.FirstName);
        response.LastName.Should().Be(updateRequest.LastName);
        response.PhoneNumber.Should().Be(updateRequest.PhoneNumber);
        response.HourlyRate.Should().Be(updateRequest.HourlyRate);
        response.Status.Should().Be(updateRequest.Status.ToString());

        // Verify changes were persisted
        var updatedTechnician = await Context.Technicians.FindAsync(technician.Id);
        updatedTechnician!.FirstName.Should().Be(updateRequest.FirstName);
        updatedTechnician.LastName.Should().Be(updateRequest.LastName);
        updatedTechnician.Status.Should().Be(updateRequest.Status.Value);
    }

    [Fact]
    public async Task AddSkill_ShouldAddSkillToTechnician()
    {
        // Arrange
        var technician = CreateTestTechnician();
        Context.Technicians.Add(technician);
        await Context.SaveChangesAsync();

        var skillRequest = new AddSkillRequest
        {
            SkillName = "HVAC",
            ProficiencyLevel = 4
        };

        // Act
        var response = await PostAsync<TechnicianResponse>($"/api/technicians/{technician.Id}/skills", skillRequest);

        // Assert
        response.Skills.Should().ContainKey("HVAC");
        response.Skills["HVAC"].Should().Be(4);

        // Verify skill was persisted
        var updatedTechnician = await Context.Technicians.FindAsync(technician.Id);
        updatedTechnician!.HasSkill("HVAC").Should().BeTrue();
        updatedTechnician.GetSkillLevel("HVAC").Should().Be(4);
    }

    [Fact]
    public async Task UpdateSkill_ShouldUpdateExistingSkill()
    {
        // Arrange
        var technician = CreateTestTechnician(); // Has Electrical at level 5
        Context.Technicians.Add(technician);
        await Context.SaveChangesAsync();

        var skillRequest = new UpdateSkillRequest
        {
            ProficiencyLevel = 3
        };

        // Act
        var response = await PutAsync<TechnicianResponse>($"/api/technicians/{technician.Id}/skills/Electrical", skillRequest);

        // Assert
        response.Skills["Electrical"].Should().Be(3);

        // Verify skill was updated
        var updatedTechnician = await Context.Technicians.FindAsync(technician.Id);
        updatedTechnician!.GetSkillLevel("Electrical").Should().Be(3);
    }

    [Fact]
    public async Task RemoveSkill_ShouldRemoveSkillFromTechnician()
    {
        // Arrange
        var technician = CreateTestTechnician(); // Has Electrical and Plumbing
        Context.Technicians.Add(technician);
        await Context.SaveChangesAsync();

        // Act
        await DeleteAsync($"/api/technicians/{technician.Id}/skills/Electrical");

        // Assert
        var updatedTechnician = await Context.Technicians.FindAsync(technician.Id);
        updatedTechnician!.HasSkill("Electrical").Should().BeFalse();
        updatedTechnician.HasSkill("Plumbing").Should().BeTrue(); // Other skills should remain
    }

    [Fact]
    public async Task GetTechnicianSchedule_ShouldReturnSchedule()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var job = CreateTestServiceJob();
        job.AssignTechnician(technician.Id);
        
        Context.Technicians.Add(technician);
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        var dateFrom = DateTime.Today;
        var dateTo = DateTime.Today.AddDays(7);

        // Act
        var response = await GetAsync<List<ScheduleItemResponse>>($"/api/technicians/{technician.Id}/schedule?dateFrom={dateFrom:yyyy-MM-dd}&dateTo={dateTo:yyyy-MM-dd}");

        // Assert
        response.Should().HaveCount(1);
        response.First().JobId.Should().Be(job.Id);
        response.First().TechnicianId.Should().Be(technician.Id);
    }

    [Fact]
    public async Task GetTechnicianAvailability_ShouldReturnAvailability()
    {
        // Arrange
        var technician = CreateTestTechnician();
        Context.Technicians.Add(technician);
        await Context.SaveChangesAsync();

        var date = DateTime.Today.AddDays(1);

        // Act
        var response = await GetAsync<AvailabilityResponse>($"/api/technicians/{technician.Id}/availability/{date:yyyy-MM-dd}");

        // Assert
        response.TechnicianId.Should().Be(technician.Id);
        response.Date.Should().Be(date);
        response.IsAvailable.Should().BeTrue();
        response.AvailableHours.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateTechnicianStatus_ShouldUpdateStatus()
    {
        // Arrange
        var technician = CreateTestTechnician();
        Context.Technicians.Add(technician);
        await Context.SaveChangesAsync();

        var statusRequest = new UpdateTechnicianStatusRequest
        {
            Status = TechnicianStatus.OnBreak
        };

        // Act
        var response = await PostAsync<TechnicianResponse>($"/api/technicians/{technician.Id}/status", statusRequest);

        // Assert
        response.Status.Should().Be("OnBreak");

        // Verify status was persisted
        var updatedTechnician = await Context.Technicians.FindAsync(technician.Id);
        updatedTechnician!.Status.Should().Be(TechnicianStatus.OnBreak);
    }

    [Fact]
    public async Task GetAvailableTechnicians_ForJob_ShouldReturnQualifiedTechnicians()
    {
        // Arrange
        var technician1 = CreateTestTechnician(); // Has Electrical, Plumbing
        var technician2 = CreateTestTechnician();
        technician2.AddSkill("HVAC", 4);
        
        var job = CreateTestServiceJob();
        job.AddRequiredSkill("Electrical"); // Only technician1 has this skill
        
        Context.Technicians.AddRange(technician1, technician2);
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<List<TechnicianResponse>>($"/api/technicians/available/{job.Id}");

        // Assert
        response.Should().HaveCount(1);
        response.First().Id.Should().Be(technician1.Id);
        response.First().Skills.Should().ContainKey("Electrical");
    }

    [Fact]
    public async Task SearchTechnicians_WithLocationAndSkills_ShouldReturnMatchingTechnicians()
    {
        // Arrange
        var technician1 = CreateTestTechnician();
        var technician2 = CreateTestTechnician();
        technician2.AddSkill("HVAC", 4);
        
        Context.Technicians.AddRange(technician1, technician2);
        await Context.SaveChangesAsync();

        var searchRequest = new SearchTechniciansRequest
        {
            Location = new Coordinate(40.7589, -73.9851), // NYC
            RadiusKm = 50,
            RequiredSkills = new List<string> { "HVAC" }
        };

        // Act
        var response = await PostAsync<List<TechnicianResponse>>("/api/technicians/search", searchRequest);

        // Assert
        response.Should().HaveCount(1);
        response.First().Id.Should().Be(technician2.Id);
        response.First().Skills.Should().ContainKey("HVAC");
    }

    [Fact]
    public async Task GetTechnicianPerformance_ShouldReturnMetrics()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var job = CreateTestServiceJob();
        job.AssignTechnician(technician.Id);
        job.UpdateStatus(JobStatus.Completed);
        
        Context.Technicians.Add(technician);
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<TechnicianPerformanceResponse>($"/api/technicians/{technician.Id}/performance");

        // Assert
        response.TechnicianId.Should().Be(technician.Id);
        response.CompletedJobs.Should().BeGreaterOrEqualTo(1);
        response.TotalRevenue.Should().BeGreaterThan(0);
        response.AverageJobRating.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task DeleteTechnician_ShouldDeactivateTechnician()
    {
        // Arrange
        var technician = CreateTestTechnician();
        Context.Technicians.Add(technician);
        await Context.SaveChangesAsync();

        // Act
        await DeleteAsync($"/api/technicians/{technician.Id}");

        // Assert
        // Technician should be deactivated, not deleted
        var deactivatedTechnician = await Context.Technicians.FindAsync(technician.Id);
        deactivatedTechnician.Should().NotBeNull();
        deactivatedTechnician!.Status.Should().Be(TechnicianStatus.Inactive);
    }
}

// DTOs for testing - these should match your actual DTOs from the API project
public record PaginatedTechniciansResponse
{
    public List<TechnicianResponse> Technicians { get; init; } = new();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record TechnicianResponse
{
    public Guid Id { get; init; }
    public string EmployeeId { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public AddressResponse Address { get; init; } = new();
    public Dictionary<string, int> Skills { get; init; } = new();
    public decimal HourlyRate { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CreateTechnicianRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string PhoneNumber { get; init; }
    public required AddressRequest Address { get; init; }
    public Dictionary<string, int> Skills { get; init; } = new();
    public decimal HourlyRate { get; init; }
    public required DateTime StartDate { get; init; }
}

public record UpdateTechnicianRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? PhoneNumber { get; init; }
    public AddressRequest? Address { get; init; }
    public decimal? HourlyRate { get; init; }
    public TechnicianStatus? Status { get; init; }
}

public record AddSkillRequest
{
    public required string SkillName { get; init; }
    public required int ProficiencyLevel { get; init; }
}

public record UpdateSkillRequest
{
    public required int ProficiencyLevel { get; init; }
}

public record UpdateTechnicianStatusRequest
{
    public required TechnicianStatus Status { get; init; }
}

public record SearchTechniciansRequest
{
    public Coordinate? Location { get; init; }
    public double RadiusKm { get; init; } = 25;
    public List<string>? RequiredSkills { get; init; }
    public TechnicianStatus? Status { get; init; }
    public DateTime? AvailableFrom { get; init; }
    public DateTime? AvailableTo { get; init; }
}

public record ScheduleItemResponse
{
    public Guid JobId { get; init; }
    public Guid TechnicianId { get; init; }
    public DateTime ScheduledDate { get; init; }
    public TimeSpan EstimatedDuration { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string JobDescription { get; init; } = string.Empty;
    public AddressResponse JobAddress { get; init; } = new();
}

public record AvailabilityResponse
{
    public Guid TechnicianId { get; init; }
    public DateTime Date { get; init; }
    public bool IsAvailable { get; init; }
    public int AvailableHours { get; init; }
    public List<TimeSlot> AvailableSlots { get; init; } = new();
    public List<ScheduleItemResponse> ScheduledJobs { get; init; } = new();
}

public record TimeSlot
{
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
}

public record TechnicianPerformanceResponse
{
    public Guid TechnicianId { get; init; }
    public int CompletedJobs { get; init; }
    public decimal TotalRevenue { get; init; }
    public double AverageJobRating { get; init; }
    public TimeSpan AverageJobDuration { get; init; }
    public int JobsOnTime { get; init; }
    public double OnTimePercentage { get; init; }
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
}
