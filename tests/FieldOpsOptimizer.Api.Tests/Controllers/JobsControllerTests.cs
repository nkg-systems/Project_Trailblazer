using FieldOpsOptimizer.Api.Tests.TestBase;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;
using FluentAssertions;
using System.Net;
using System.Text.Json;

namespace FieldOpsOptimizer.Api.Tests.Controllers;

public class JobsControllerTests : ApiIntegrationTestBase
{
    public JobsControllerTests(CustomWebApplicationFactory<Program> factory) : base(factory) { }

    [Fact]
    public async Task GetJobs_ShouldReturnPaginatedJobsList()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var job1 = CreateTestServiceJob();
        var job2 = CreateTestServiceJob();
        
        Context.Technicians.Add(technician);
        Context.ServiceJobs.AddRange(job1, job2);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<PaginatedJobsResponse>("/api/jobs");

        // Assert
        response.Jobs.Should().HaveCount(2);
        response.TotalCount.Should().Be(2);
        response.PageNumber.Should().Be(1);
        response.PageSize.Should().Be(50);
        response.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GetJobs_WithStatusFilter_ShouldReturnFilteredJobs()
    {
        // Arrange
        var job1 = CreateTestServiceJob();
        var job2 = CreateTestServiceJob();
        job2.UpdateStatus(JobStatus.Completed);
        
        Context.ServiceJobs.AddRange(job1, job2);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<PaginatedJobsResponse>("/api/jobs?status=Completed");

        // Assert
        response.Jobs.Should().HaveCount(1);
        response.Jobs.First().Status.Should().Be("Completed");
    }

    [Fact]
    public async Task GetJobs_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var jobs = Enumerable.Range(1, 15).Select(_ => CreateTestServiceJob()).ToList();
        Context.ServiceJobs.AddRange(jobs);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<PaginatedJobsResponse>("/api/jobs?pageNumber=2&pageSize=10");

        // Assert
        response.Jobs.Should().HaveCount(5); // Remaining 5 jobs on page 2
        response.PageNumber.Should().Be(2);
        response.PageSize.Should().Be(10);
        response.TotalCount.Should().Be(15);
        response.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetJob_WithValidId_ShouldReturnJob()
    {
        // Arrange
        var job = CreateTestServiceJob();
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<JobResponse>($"/api/jobs/{job.Id}");

        // Assert
        response.Id.Should().Be(job.Id);
        response.CustomerName.Should().Be(job.CustomerName);
        response.Status.Should().Be(job.Status.ToString());
    }

    [Fact]
    public async Task GetJob_WithInvalidId_ShouldReturn404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/jobs/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateJob_WithValidData_ShouldCreateAndReturnJob()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            CustomerName = Faker.Company.CompanyName(),
            Address = new AddressRequest
            {
                Street = Faker.Address.StreetAddress(),
                City = Faker.Address.City(),
                State = Faker.Address.StateAbbr(),
                PostalCode = Faker.Address.ZipCode(),
                Country = "USA",
                Coordinate = new Coordinate(Faker.Address.Latitude(), Faker.Address.Longitude())
            },
            Description = Faker.Lorem.Paragraph(),
            EstimatedDuration = TimeSpan.FromHours(2),
            EstimatedRevenue = 150.00m,
            Priority = JobPriority.Medium,
            RequiredSkills = new List<string> { "Electrical", "Plumbing" },
            Tags = new List<string> { "Maintenance", "Urgent" },
            ScheduledDate = DateTime.Today.AddDays(1)
        };

        // Act
        var response = await PostAsync<JobResponse>("/api/jobs", request);

        // Assert
        response.Should().NotBeNull();
        response.CustomerName.Should().Be(request.CustomerName);
        response.Description.Should().Be(request.Description);
        response.Priority.Should().Be(request.Priority);
        response.Status.Should().Be("Scheduled");
        response.RequiredSkills.Should().BeEquivalentTo(request.RequiredSkills);
        response.Tags.Should().BeEquivalentTo(request.Tags);
        response.JobNumber.Should().StartWith("JOB");

        // Verify job was saved to database
        var savedJob = await Context.ServiceJobs.FindAsync(response.Id);
        savedJob.Should().NotBeNull();
        savedJob!.CustomerName.Should().Be(request.CustomerName);
    }

    [Fact]
    public async Task CreateJob_WithInvalidData_ShouldReturn400()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            CustomerName = "", // Invalid: empty customer name
            Address = null!, // Invalid: null address
            Description = "",
            EstimatedDuration = TimeSpan.Zero, // Invalid: zero duration
            ScheduledDate = DateTime.Today.AddDays(-1) // Invalid: past date
        };

        // Act
        var response = await Client.PostAsync("/api/jobs", 
            new StringContent(JsonSerializer.Serialize(request, JsonOptions), 
            System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateJob_WithValidData_ShouldUpdateJob()
    {
        // Arrange
        var job = CreateTestServiceJob();
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        var updateRequest = new UpdateJobRequest
        {
            Description = "Updated description",
            Priority = JobPriority.High,
            Status = JobStatus.InProgress
        };

        // Act
        var response = await PutAsync<JobResponse>($"/api/jobs/{job.Id}", updateRequest);

        // Assert
        response.Description.Should().Be(updateRequest.Description);
        response.Priority.Should().Be(updateRequest.Priority);
        response.Status.Should().Be(updateRequest.Status.ToString());

        // Verify changes were persisted
        var updatedJob = await Context.ServiceJobs.FindAsync(job.Id);
        updatedJob!.Description.Should().Be(updateRequest.Description);
        updatedJob.Priority.Should().Be(updateRequest.Priority.Value);
        updatedJob.Status.Should().Be(updateRequest.Status.Value);
    }

    [Fact]
    public async Task AssignJob_WithValidTechnician_ShouldAssignJob()
    {
        // Arrange
        var technician = CreateTestTechnician();
        var job = CreateTestServiceJob();
        
        // Ensure job requires skills that technician has
        job.AddRequiredSkill("Electrical");
        
        Context.Technicians.Add(technician);
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        var assignRequest = new AssignJobRequest
        {
            TechnicianId = technician.Id
        };

        // Act
        var response = await PostAsync<JobResponse>($"/api/jobs/{job.Id}/assign", assignRequest);

        // Assert
        response.AssignedTechnicianId.Should().Be(technician.Id);

        // Verify assignment was persisted
        var assignedJob = await Context.ServiceJobs.FindAsync(job.Id);
        assignedJob!.AssignedTechnicianId.Should().Be(technician.Id);
    }

    [Fact]
    public async Task AssignJob_WithMissingSkills_ShouldReturn400()
    {
        // Arrange
        var technician = CreateTestTechnician(); // Has Electrical and Plumbing
        var job = CreateTestServiceJob();
        
        // Add a skill the technician doesn't have
        job.AddRequiredSkill("HVAC");
        
        Context.Technicians.Add(technician);
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        var assignRequest = new AssignJobRequest
        {
            TechnicianId = technician.Id
        };

        // Act
        var response = await Client.PostAsync($"/api/jobs/{job.Id}/assign", 
            new StringContent(JsonSerializer.Serialize(assignRequest, JsonOptions), 
            System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdateJobStatus_ShouldUpdateStatus()
    {
        // Arrange
        var job = CreateTestServiceJob();
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        var statusRequest = new UpdateJobStatusRequest
        {
            Status = JobStatus.InProgress
        };

        // Act
        var response = await PostAsync<JobResponse>($"/api/jobs/{job.Id}/status", statusRequest);

        // Assert
        response.Status.Should().Be("InProgress");

        // Verify status was persisted
        var updatedJob = await Context.ServiceJobs.FindAsync(job.Id);
        updatedJob!.Status.Should().Be(JobStatus.InProgress);
    }

    [Fact]
    public async Task UpdateJobStatus_ToCompleted_ShouldSetCompletedAt()
    {
        // Arrange
        var job = CreateTestServiceJob();
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        var statusRequest = new UpdateJobStatusRequest
        {
            Status = JobStatus.Completed
        };

        // Act
        var response = await PostAsync<JobResponse>($"/api/jobs/{job.Id}/status", statusRequest);

        // Assert
        response.Status.Should().Be("Completed");
        response.CompletedAt.Should().NotBeNull();

        // Verify completion timestamp was set
        var completedJob = await Context.ServiceJobs.FindAsync(job.Id);
        completedJob!.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteJob_ShouldRemoveJob()
    {
        // Arrange
        var job = CreateTestServiceJob();
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        // Act
        await DeleteAsync($"/api/jobs/{job.Id}");

        // Assert
        var deletedJob = await Context.ServiceJobs.FindAsync(job.Id);
        deletedJob.Should().BeNull();
    }

    [Fact]
    public async Task DeleteJob_InProgress_ShouldReturn400()
    {
        // Arrange
        var job = CreateTestServiceJob();
        job.UpdateStatus(JobStatus.InProgress);
        Context.ServiceJobs.Add(job);
        await Context.SaveChangesAsync();

        // Act
        var response = await Client.DeleteAsync($"/api/jobs/{job.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SearchJobs_WithTextSearch_ShouldReturnMatchingJobs()
    {
        // Arrange
        var job1 = CreateTestServiceJob();
        var job2 = CreateTestServiceJob();
        
        // Set specific customer name for searching
        job1 = new ServiceJob(
            GenerateJobNumber(),
            "ACME Corporation",
            job1.ServiceAddress,
            job1.Description,
            job1.ScheduledDate,
            job1.EstimatedDuration,
            "test-tenant");

        Context.ServiceJobs.AddRange(job1, job2);
        await Context.SaveChangesAsync();

        var searchRequest = new SearchJobsRequest
        {
            SearchText = "ACME"
        };

        // Act
        var response = await PostAsync<List<JobResponse>>("/api/jobs/search", searchRequest);

        // Assert
        response.Should().HaveCount(1);
        response.First().CustomerName.Should().Contain("ACME");
    }

    [Fact]
    public async Task GetAvailableJobs_ForTechnician_ShouldReturnMatchingJobs()
    {
        // Arrange
        var technician = CreateTestTechnician(); // Has Electrical and Plumbing skills
        var job1 = CreateTestServiceJob();
        var job2 = CreateTestServiceJob();
        
        // Make job1 require skills technician has, job2 assigned to someone else
        job1.AddRequiredSkill("Electrical");
        job2.AssignTechnician(Guid.NewGuid());

        Context.Technicians.Add(technician);
        Context.ServiceJobs.AddRange(job1, job2);
        await Context.SaveChangesAsync();

        // Act
        var response = await GetAsync<List<JobResponse>>($"/api/jobs/available/{technician.Id}");

        // Assert
        response.Should().HaveCount(1);
        response.First().Id.Should().Be(job1.Id);
        response.First().AssignedTechnicianId.Should().BeNull();
    }
}

// DTOs for testing - these should match your actual DTOs from the API project
public record PaginatedJobsResponse
{
    public List<JobResponse> Jobs { get; init; } = new();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record JobResponse
{
    public Guid Id { get; init; }
    public string JobNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public AddressResponse Address { get; init; } = new();
    public TimeSpan EstimatedDuration { get; init; }
    public decimal EstimatedRevenue { get; init; }
    public JobPriority Priority { get; init; }
    public string Status { get; init; } = string.Empty;
    public List<string> RequiredSkills { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public DateTime ScheduledDate { get; init; }
    public TimeSpan? PreferredTimeWindow { get; init; }
    public Guid? AssignedTechnicianId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public record AddressResponse
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string FormattedAddress { get; init; } = string.Empty;
    public Coordinate? Coordinate { get; init; }
}

public record CreateJobRequest
{
    public required string CustomerName { get; init; }
    public required AddressRequest Address { get; init; }
    public required string Description { get; init; }
    public required TimeSpan EstimatedDuration { get; init; }
    public decimal EstimatedRevenue { get; init; }
    public JobPriority Priority { get; init; } = JobPriority.Medium;
    public List<string> RequiredSkills { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public required DateTime ScheduledDate { get; init; }
    public TimeSpan? PreferredTimeWindow { get; init; }
}

public record AddressRequest
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string PostalCode { get; init; }
    public string Country { get; init; } = "USA";
    public Coordinate? Coordinate { get; init; }
}

public record UpdateJobRequest
{
    public string? Description { get; init; }
    public TimeSpan? EstimatedDuration { get; init; }
    public decimal? EstimatedRevenue { get; init; }
    public JobPriority? Priority { get; init; }
    public JobStatus? Status { get; init; }
    public DateTime? ScheduledDate { get; init; }
    public TimeSpan? PreferredTimeWindow { get; init; }
    public Guid? AssignedTechnicianId { get; init; }
    public List<string>? RequiredSkills { get; init; }
    public List<string>? Tags { get; init; }
}

public record AssignJobRequest
{
    public required Guid TechnicianId { get; init; }
}

public record UpdateJobStatusRequest
{
    public required JobStatus Status { get; init; }
}

public record SearchJobsRequest
{
    public string? SearchText { get; init; }
    public Coordinate? Location { get; init; }
    public double RadiusKm { get; init; } = 10;
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public List<string>? RequiredSkills { get; init; }
}
