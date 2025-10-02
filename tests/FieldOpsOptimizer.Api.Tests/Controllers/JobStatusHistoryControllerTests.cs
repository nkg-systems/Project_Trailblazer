using FieldOpsOptimizer.Api.Tests.TestBase;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace FieldOpsOptimizer.Api.Tests.Controllers;

public class JobStatusHistoryControllerTests : ApiIntegrationTestBase
{
    private ServiceJob _testServiceJob = null!;
    private const string TestTenantId = "test-tenant";

    public JobStatusHistoryControllerTests(CustomWebApplicationFactory<Program> factory) : base(factory)
    {
    }

    protected override async Task SeedTestDataAsync()
    {
        // Create test service job
        _testServiceJob = CreateTestServiceJob();
        Context.ServiceJobs.Add(_testServiceJob);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetJobStatusHistory_ShouldReturnHistory_WhenJobHasHistory()
    {
        // Arrange
        var history1 = new JobStatusHistory(
            _testServiceJob.Id,
            _testServiceJob.JobNumber,
            TestTenantId,
            JobStatus.Scheduled,
            JobStatus.InProgress,
            Guid.NewGuid(),
            "Test User 1",
            reason: "Job started by technician");

        var history2 = new JobStatusHistory(
            _testServiceJob.Id,
            _testServiceJob.JobNumber,
            TestTenantId,
            JobStatus.InProgress,
            JobStatus.Completed,
            Guid.NewGuid(),
            "Test User 2",
            reason: "Job completed successfully");

        Context.JobStatusHistory.AddRange(history1, history2);
        await Context.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/jobstatushistory?serviceJobId={_testServiceJob.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var historyEntries = JsonSerializer.Deserialize<List<JobStatusHistoryDto>>(content, JsonOptions);
        
        historyEntries.Should().HaveCount(2);
        historyEntries.Should().Contain(h => h.FromStatus == JobStatus.Scheduled && h.ToStatus == JobStatus.InProgress);
        historyEntries.Should().Contain(h => h.FromStatus == JobStatus.InProgress && h.ToStatus == JobStatus.Completed);
    }

    [Fact]
    public async Task CreateJobStatusHistory_ShouldReturnCreatedEntry_WhenDataIsValid()
    {
        // Arrange
        var request = new CreateJobStatusHistoryRequest
        {
            ServiceJobId = _testServiceJob.Id,
            JobNumber = _testServiceJob.JobNumber,
            FromStatus = JobStatus.Scheduled,
            ToStatus = JobStatus.InProgress,
            Reason = "Job started by technician",
            IsAutomaticChange = false
        };

        // Act
        var response = await Client.PostAsync("/api/jobstatushistory", CreateJsonContent(request));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var createdHistory = JsonSerializer.Deserialize<JobStatusHistoryDto>(content, JsonOptions);

        createdHistory.Should().NotBeNull();
        createdHistory!.ServiceJobId.Should().Be(_testServiceJob.Id);
        createdHistory.FromStatus.Should().Be(JobStatus.Scheduled);
        createdHistory.ToStatus.Should().Be(JobStatus.InProgress);
        createdHistory.Reason.Should().Be("Job started by technician");
        createdHistory.IsAutomaticChange.Should().BeFalse();
    }

    [Fact]
    public async Task GetJobStatusHistoryStats_ShouldReturnCorrectStats_WhenJobHasHistory()
    {
        // Arrange
        var manualChange1 = new JobStatusHistory(
            _testServiceJob.Id,
            _testServiceJob.JobNumber,
            TestTenantId,
            JobStatus.Scheduled,
            JobStatus.InProgress,
            Guid.NewGuid(),
            "Test User 1",
            isAutomaticChange: false);

        var manualChange2 = new JobStatusHistory(
            _testServiceJob.Id,
            _testServiceJob.JobNumber,
            TestTenantId,
            JobStatus.InProgress,
            JobStatus.Completed,
            Guid.NewGuid(),
            "Test User 2",
            isAutomaticChange: false);

        var automaticChange = new JobStatusHistory(
            _testServiceJob.Id,
            _testServiceJob.JobNumber,
            TestTenantId,
            JobStatus.Completed,
            JobStatus.Cancelled,
            Guid.NewGuid(),
            "System",
            isAutomaticChange: true);

        Context.JobStatusHistory.AddRange(manualChange1, manualChange2, automaticChange);
        await Context.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/jobstatushistory/{_testServiceJob.Id}/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<JobStatusHistoryStatsDto>(content, JsonOptions);

        stats.Should().NotBeNull();
        stats!.TotalTransitions.Should().Be(3);
        stats.Manual.Should().Be(2);
        stats.Automatic.Should().Be(1);
    }

    [Fact]
    public async Task GetJobStatusHistoryByStatus_ShouldReturnFilteredResults_WhenStatusFilterApplied()
    {
        // Arrange
        var historyToInProgress = new JobStatusHistory(
            _testServiceJob.Id,
            _testServiceJob.JobNumber,
            TestTenantId,
            JobStatus.Scheduled,
            JobStatus.InProgress,
            Guid.NewGuid(),
            "Test User 1");

        var historyToCompleted = new JobStatusHistory(
            _testServiceJob.Id,
            _testServiceJob.JobNumber,
            TestTenantId,
            JobStatus.InProgress,
            JobStatus.Completed,
            Guid.NewGuid(),
            "Test User 2");

        var anotherToInProgress = new JobStatusHistory(
            Guid.NewGuid(), // Different job
            "OTHER-001",
            TestTenantId,
            JobStatus.Scheduled,
            JobStatus.InProgress,
            Guid.NewGuid(),
            "Test User 3");

        Context.JobStatusHistory.AddRange(historyToInProgress, historyToCompleted, anotherToInProgress);
        await Context.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/jobstatushistory?toStatus={JobStatus.InProgress}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var historyEntries = JsonSerializer.Deserialize<List<JobStatusHistoryDto>>(content, JsonOptions);

        historyEntries.Should().HaveCount(2);
        historyEntries.Should().OnlyContain(h => h.ToStatus == JobStatus.InProgress);
    }

    [Fact]
    public async Task GetJobStatusHistoryByDateRange_ShouldReturnFilteredResults_WhenDateRangeProvided()
    {
        // Arrange
        var oldHistory = new JobStatusHistory(
            _testServiceJob.Id,
            _testServiceJob.JobNumber,
            TestTenantId,
            JobStatus.Scheduled,
            JobStatus.InProgress,
            Guid.NewGuid(),
            "Test User 1");

        var recentHistory = new JobStatusHistory(
            _testServiceJob.Id,
            _testServiceJob.JobNumber,
            TestTenantId,
            JobStatus.InProgress,
            JobStatus.Completed,
            Guid.NewGuid(),
            "Test User 2");

        Context.JobStatusHistory.AddRange(oldHistory, recentHistory);
        await Context.SaveChangesAsync();

        // Manually update the ChangedAt to simulate time differences
        oldHistory.GetType().GetProperty("ChangedAt")?.SetValue(oldHistory, DateTime.UtcNow.AddDays(-10));
        recentHistory.GetType().GetProperty("ChangedAt")?.SetValue(recentHistory, DateTime.UtcNow.AddHours(-1));
        await Context.SaveChangesAsync();

        var fromDate = DateTime.UtcNow.AddDays(-2);
        var toDate = DateTime.UtcNow;

        // Act
        var response = await Client.GetAsync($"/api/jobstatushistory?changedFrom={fromDate:yyyy-MM-ddTHH:mm:ss}Z&changedTo={toDate:yyyy-MM-ddTHH:mm:ss}Z");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var historyEntries = JsonSerializer.Deserialize<List<JobStatusHistoryDto>>(content, JsonOptions);

        historyEntries.Should().HaveCount(1);
        historyEntries.Should().Contain(h => h.ToStatus == JobStatus.Completed);
    }

    protected override async Task CleanupTestDataAsync()
    {
        Context.JobStatusHistory.RemoveRange(Context.JobStatusHistory);
        Context.ServiceJobs.RemoveRange(Context.ServiceJobs);
        await Context.SaveChangesAsync();
    }

    private static System.Net.Http.StringContent CreateJsonContent(object obj)
    {
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    // DTOs for API responses - these should match the actual API DTOs
    public class JobStatusHistoryDto
    {
        public Guid Id { get; set; }
        public Guid ServiceJobId { get; set; }
        public string JobNumber { get; set; } = string.Empty;
        public JobStatus FromStatus { get; set; }
        public JobStatus ToStatus { get; set; }
        public DateTime ChangedAt { get; set; }
        public string ChangedByUserName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public bool IsAutomaticChange { get; set; }
        public string? ChangeSource { get; set; }
        public int? PreviousStatusDurationMinutes { get; set; }
    }

    public class CreateJobStatusHistoryRequest
    {
        public Guid ServiceJobId { get; set; }
        public string JobNumber { get; set; } = string.Empty;
        public JobStatus FromStatus { get; set; }
        public JobStatus ToStatus { get; set; }
        public string? Reason { get; set; }
        public bool IsAutomaticChange { get; set; }
        public string? ChangeSource { get; set; }
    }

    public class JobStatusHistoryStatsDto
    {
        public int TotalTransitions { get; set; }
        public int Manual { get; set; }
        public int Automatic { get; set; }
    }
}