using AutoFixture;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Common;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FieldOpsOptimizer.Application.Tests.Services;

public class JobStatusHistoryServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
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
        _mockTenantService = new Mock<ITenantService>();
        
        _mockTenantService.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);
        
        _jobStatusHistoryService = new JobStatusHistoryService(_context, _mockTenantService.Object);
        
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Fact]
    public async Task RecordAsync_ShouldCreateHistoryEntry_WhenValidData()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);
        await _context.SaveChangesAsync();

        var fromStatus = JobStatus.Scheduled;
        var toStatus = JobStatus.InProgress;
        var changedByUserId = Guid.NewGuid();
        var changedByUserName = "Status Changer";
        var reason = "Job started by technician";

        // Act
        var result = await _jobStatusHistoryService.RecordAsync(
            serviceJob.Id,
            serviceJob.JobNumber,
            fromStatus,
            toStatus,
            changedByUserId,
            changedByUserName,
            reason: reason);

        // Assert
        result.Should().NotBeNull();
        result.ServiceJobId.Should().Be(serviceJob.Id);
        result.FromStatus.Should().Be(fromStatus);
        result.ToStatus.Should().Be(toStatus);
        result.ChangedByUserId.Should().Be(changedByUserId);
        result.ChangedByUserName.Should().Be(changedByUserName);
        result.Reason.Should().Be(reason);
        result.TenantId.Should().Be(_testTenantId);
        result.Id.Should().NotBe(Guid.Empty);
        result.ChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordAsync_ShouldThrowArgumentException_WhenUserNameInvalid(string invalidUserName)
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);
        await _context.SaveChangesAsync();

        var fromStatus = JobStatus.Scheduled;
        var toStatus = JobStatus.InProgress;
        var changedByUserId = Guid.NewGuid();

        // Act & Assert
        await FluentActions.Invoking(() => _jobStatusHistoryService.RecordAsync(
                serviceJob.Id,
                serviceJob.JobNumber,
                fromStatus,
                toStatus,
                changedByUserId,
                invalidUserName))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByServiceJobAsync_ShouldReturnHistoryForJob_WhenJobExists()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var history1 = CreateTestStatusHistory(serviceJob.Id, JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(serviceJob.Id, JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress); // Different job

        await _context.JobStatusHistory.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.GetByServiceJobAsync(serviceJob.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(h => h.ToStatus == JobStatus.InProgress);
        result.Should().Contain(h => h.ToStatus == JobStatus.Completed);
        result.Should().NotContain(h => h.ServiceJobId != serviceJob.Id);
    }

    [Fact]
    public async Task FilterAsync_ShouldFilterByServiceJob_WhenServiceJobSpecified()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var history1 = CreateTestStatusHistory(serviceJob.Id, JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(serviceJob.Id, JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress); // Different job

        await _context.JobStatusHistory.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.FilterAsync(serviceJobId: serviceJob.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(h => h.ServiceJobId == serviceJob.Id);
        result.Should().NotContain(h => h.ServiceJobId != serviceJob.Id);
    }

    [Fact]
    public async Task FilterAsync_ShouldFilterByFromStatus_WhenFromStatusSpecified()
    {
        // Arrange
        var history1 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Completed, JobStatus.Cancelled);

        await _context.JobStatusHistory.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.FilterAsync(fromStatus: JobStatus.Scheduled);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(h => h.FromStatus == JobStatus.Scheduled);
        result.Should().NotContain(h => h.FromStatus != JobStatus.Scheduled);
    }

    [Fact]
    public async Task FilterAsync_ShouldFilterByToStatus_WhenToStatusSpecified()
    {
        // Arrange
        var history1 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(Guid.NewGuid(), JobStatus.Completed, JobStatus.Cancelled);

        await _context.JobStatusHistory.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobStatusHistoryService.FilterAsync(toStatus: JobStatus.InProgress);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(h => h.ToStatus == JobStatus.InProgress);
        result.Should().NotContain(h => h.ToStatus != JobStatus.InProgress);
    }

    [Fact]
    public async Task GetStatsAsync_ShouldReturnStats_WhenDataExists()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var history1 = CreateTestStatusHistory(serviceJob.Id, JobStatus.Scheduled, JobStatus.InProgress);
        var history2 = CreateTestStatusHistory(serviceJob.Id, JobStatus.InProgress, JobStatus.Completed);
        var history3 = CreateTestStatusHistory(serviceJob.Id, JobStatus.Completed, JobStatus.Cancelled, isAutomaticChange: true);

        await _context.JobStatusHistory.AddRangeAsync(history1, history2, history3);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _jobStatusHistoryService.GetStatsAsync(serviceJob.Id);

        // Assert
        stats.TotalTransitions.Should().Be(3);
        stats.Automatic.Should().Be(1);
        stats.Manual.Should().Be(2);
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

    private JobStatusHistory CreateTestStatusHistory(Guid? serviceJobId = null, 
        JobStatus fromStatus = JobStatus.Scheduled, 
        JobStatus toStatus = JobStatus.InProgress,
        Guid? changedByUserId = null,
        string changedByUserName = "Test User",
        string reason = "Test status change",
        bool isAutomaticChange = false)
    {
        return new JobStatusHistory(
            serviceJobId ?? Guid.NewGuid(),
            $"JOB-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
            _testTenantId,
            fromStatus,
            toStatus,
            changedByUserId ?? Guid.NewGuid(),
            changedByUserName,
            reason: reason,
            isAutomaticChange: isAutomaticChange,
            auditInfo: new AuditInfo("127.0.0.1", "Test Agent", "test-session")
        );
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}