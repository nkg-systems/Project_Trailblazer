using AutoFixture;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FieldOpsOptimizer.Application.Tests.Services;

public class JobStatusHistoryServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<JobStatusHistoryService>> _mockLogger;
    private readonly Mock<ITenantService> _mockTenantService;
    private readonly JobStatusHistoryService _jobStatusHistoryService;
    private readonly IFixture _fixture;
    private readonly string _testTenantId = "test-tenant-456";

    public JobStatusHistoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<JobStatusHistoryService>>();
        _mockTenantService = new Mock<ITenantService>();
        
        _mockTenantService.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);
        
        _jobStatusHistoryService = new JobStatusHistoryService(_context, _mockLogger.Object, _mockTenantService.Object);
        
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Fact]
    public async Task RecordStatusChangeAsync_ShouldCreateHistoryEntry_WhenValidData()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);
        await _context.SaveChangesAsync();

        var fromStatus = JobStatus.Scheduled;
        var toStatus = JobStatus.InProgress;
        var changedByUserId = Guid.NewGuid();
        var changedByUserName = "Status Changer";
        var changeReason = "Job started by technician";

        // Act
        var result = await _jobStatusHistoryService.RecordStatusChangeAsync(
            serviceJob.Id, 
            fromStatus, 
            toStatus, 
            changedByUserId, 
            changedByUserName,
            changeReason);

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().Be(serviceJob.Id);
        result.FromStatus.Should().Be(fromStatus);
        result.ToStatus.Should().Be(toStatus);
        result.ChangedByUserId.Should().Be(changedByUserId);
        result.ChangedByUserName.Should().Be(changedByUserName);
        result.ChangeReason.Should().Be(changeReason);
        result.TenantId.Should().Be(_testTenantId);
        result.Id.Should().NotBe(Guid.Empty);
        result.ChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordStatusChangeAsync_ShouldThrowArgumentException_WhenJobNotFound()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();
        var fromStatus = JobStatus.Scheduled;
        var toStatus = JobStatus.InProgress;
        var changedByUserId = Guid.NewGuid();
        var changedByUserName = "Status Changer";

        // Act & Assert
        await FluentActions.Invoking(() => _jobStatusHistoryService.RecordStatusChangeAsync(
                nonExistentJobId, 
                fromStatus, 
                toStatus, 
                changedByUserId, 
                changedByUserName))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage($"Service job with ID {nonExistentJobId} not found in tenant {_testTenantId}*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordStatusChangeAsync_ShouldThrowArgumentException_WhenUserNameInvalid(string invalidUserName)
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);
        await _context.SaveChangesAsync();

        var fromStatus = JobStatus.Scheduled;
        var toStatus = JobStatus.InProgress;
        var changedByUserId = Guid.NewGuid();

        // Act & Assert
        await FluentActions.Invoking(() => _jobStatusHistoryService.RecordStatusChangeAsync(
                serviceJob.Id, 
                fromStatus, 
                toStatus, 
                changedByUserId, 
                invalidUserName))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Changed by user name cannot be null or empty*");
    }

    [Fact]
    public async Task GetHistoryByIdAsync_ShouldReturnHistory_WhenHistoryExists()
    {
        // Arrange
        var historyEntry = CreateTestStatusHistory();
        await _context.JobStatusHistories.AddAsync(historyEntry);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.GetHistoryByIdAsync(historyEntry.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(historyEntry.Id);
        result.FromStatus.Should().Be(historyEntry.FromStatus);
        result.ToStatus.Should().Be(historyEntry.ToStatus);
    }

    [Fact]
    public async Task GetHistoryByIdAsync_ShouldReturnNull_WhenHistoryNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobStatusHistoryService.GetHistoryByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoryByJobIdAsync_ShouldReturnHistoryForJob_WhenJobExists()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var history1 = CreateTestStatusHistory(serviceJob.Id, JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(serviceJob.Id, JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress); // Different job

        await _context.JobStatusHistories.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.GetHistoryByJobIdAsync(serviceJob.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(h => h.ToStatus == JobStatus.Scheduled);
        result.Should().Contain(h => h.ToStatus == JobStatus.InProgress);
        result.Should().NotContain(h => h.JobId != serviceJob.Id);
    }

    [Fact]
    public async Task GetHistoryByJobIdAsync_ShouldReturnOrderedHistory_WhenMultipleEntries()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var history1 = CreateTestStatusHistory(serviceJob.Id, JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(serviceJob.Id, JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(serviceJob.Id, JobStatus.Completed, JobStatus.Cancelled);

        // Add in reverse order to test sorting
        await _context.JobStatusHistories.AddRangeAsync(history3, history1, history2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.GetHistoryByJobIdAsync(serviceJob.Id, orderByDescending: false);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        var resultList = result.ToList();
        resultList[0].ToStatus.Should().Be(JobStatus.InProgress);
        resultList[1].ToStatus.Should().Be(JobStatus.Completed);
        resultList[2].ToStatus.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public async Task GetHistoryByStatusAsync_ShouldFilterByFromStatus_WhenFromStatusSpecified()
    {
        // Arrange
        var history1 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Completed, JobStatus.Cancelled);

        await _context.JobStatusHistories.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.GetHistoryByStatusAsync(fromStatus: JobStatus.Scheduled);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(h => h.FromStatus == JobStatus.Scheduled);
        result.Should().NotContain(h => h.FromStatus != JobStatus.Scheduled);
    }

    [Fact]
    public async Task GetHistoryByStatusAsync_ShouldFilterByToStatus_WhenToStatusSpecified()
    {
        // Arrange
        var history1 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Completed, JobStatus.Cancelled);

        await _context.JobStatusHistories.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.GetHistoryByStatusAsync(toStatus: JobStatus.InProgress);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(h => h.ToStatus == JobStatus.InProgress);
        result.Should().NotContain(h => h.ToStatus != JobStatus.InProgress);
    }

    [Fact]
    public async Task GetHistoryByDateRangeAsync_ShouldFilterByDateRange_WhenDatesSpecified()
    {
        // Arrange
        var startDate = DateTime.UtcNow.Date.AddDays(-7);
        var endDate = DateTime.UtcNow.Date.AddDays(-1);
        
        var history1 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Completed, JobStatus.Cancelled);

        // Manually set dates for testing
        history1.SetChangedAt(startDate.AddDays(1));
        history2.SetChangedAt(startDate.AddDays(-2)); // Before range
        history3.SetChangedAt(endDate.AddDays(2)); // After range

        await _context.JobStatusHistories.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.GetHistoryByDateRangeAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(h => h.Id == history1.Id);
        result.Should().NotContain(h => h.Id == history2.Id);
        result.Should().NotContain(h => h.Id == history3.Id);
    }

    [Fact]
    public async Task GetHistoryByUserAsync_ShouldFilterByUser_WhenUserSpecified()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var targetUserName = "Target User";
        var otherUserId = Guid.NewGuid();
        var otherUserName = "Other User";

        var history1 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress, 
            changedByUserId: targetUserId, changedByUserName: targetUserName);
        var history2 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.InProgress, JobStatus.Completed, 
            changedByUserId: otherUserId, changedByUserName: otherUserName);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Completed, JobStatus.Cancelled, 
            changedByUserId: targetUserId, changedByUserName: targetUserName);

        await _context.JobStatusHistories.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.GetHistoryByUserAsync(targetUserId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(h => h.Id == history1.Id);
        result.Should().Contain(h => h.Id == history3.Id);
        result.Should().NotContain(h => h.Id == history2.Id);
    }

    [Fact]
    public async Task GetStatusTransitionStatsAsync_ShouldReturnStats_WhenDataExists()
    {
        // Arrange
        var history1 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.InProgress, JobStatus.Completed);
        var history4 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Completed, JobStatus.Cancelled);

        await _context.JobStatusHistories.AddRangeAsync(history1, history2, history3, history4);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.GetStatusTransitionStatsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // 3 unique transitions
        
        var scheduledToInProgress = result.FirstOrDefault(s => s.FromStatus == JobStatus.Scheduled && s.ToStatus == JobStatus.InProgress);
        scheduledToInProgress.Should().NotBeNull();
        scheduledToInProgress!.Count.Should().Be(2);

        var inProgressToCompleted = result.FirstOrDefault(s => s.FromStatus == JobStatus.InProgress && s.ToStatus == JobStatus.Completed);
        inProgressToCompleted.Should().NotBeNull();
        inProgressToCompleted!.Count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteHistoryAsync_ShouldReturnTrue_WhenHistoryExists()
    {
        // Arrange
        var historyEntry = CreateTestStatusHistory();
        await _context.JobStatusHistories.AddAsync(historyEntry);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.DeleteHistoryAsync(historyEntry.Id);

        // Assert
        result.Should().BeTrue();
        
        // Verify history is deleted
        var deletedHistory = await _context.JobStatusHistories.FindAsync(historyEntry.Id);
        deletedHistory.Should().BeNull();
    }

    [Fact]
    public async Task DeleteHistoryAsync_ShouldReturnFalse_WhenHistoryNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobStatusHistoryService.DeleteHistoryAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    private ServiceJob CreateTestServiceJob()
    {
        return new ServiceJob(
            "JOB20241201-002",
            "Test Customer 2",
            new Domain.ValueObjects.Address(
                "456 Test Ave",
                "Test City", 
                "TS",
                "67890",
                "USA",
                "456 Test Ave, Test City, TS 67890",
                new Domain.ValueObjects.Coordinate(34.0522, -118.2437)
            ),
            "Test service job for status tracking",
            DateTime.Today.AddDays(2),
            TimeSpan.FromHours(3),
            _testTenantId
        );
    }

    private JobStatusHistory CreateTestStatusHistory(Guid? jobId = null, 
        JobStatus fromStatus = JobStatus.Scheduled, 
        JobStatus toStatus = JobStatus.InProgress,
        Guid? changedByUserId = null,
        string changedByUserName = "Test User",
        string changeReason = "Test status change")
    {
        return new JobStatusHistory(
            jobId ?? Guid.NewGuid(),
            fromStatus,
            toStatus,
            changedByUserId ?? Guid.NewGuid(),
            changedByUserName,
            _testTenantId,
            changeReason
        );
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}