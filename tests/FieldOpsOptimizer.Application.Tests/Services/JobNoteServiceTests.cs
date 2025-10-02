using AutoFixture;
using AutoFixture.Xunit2;
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

public class JobNoteServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITenantService> _mockTenantService;
    private readonly JobNoteService _jobNoteService;
    private readonly IFixture _fixture;
    private readonly string _testTenantId = "test-tenant-123";

    public JobNoteServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new ApplicationDbContext(options);
        _mockTenantService = new Mock<ITenantService>();
        
        _mockTenantService.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);
        
        _jobNoteService = new JobNoteService(_context, _mockTenantService.Object);
        
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateJobNote_WhenValidData()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);
        await _context.SaveChangesAsync();

        var content = "This is a test note";
        var noteType = JobNoteType.Technical;
        var authorUserId = Guid.NewGuid();
        var authorName = "Test User";
        var isCustomerVisible = false;
        var isSensitive = false;

        // Act
        var result = await _jobNoteService.CreateAsync(
            content,
            noteType,
            serviceJob.Id,
            authorUserId,
            authorName,
            isCustomerVisible: isCustomerVisible,
            isSensitive: isSensitive);

        // Assert
        result.Should().NotBeNull();
        result.ServiceJobId.Should().Be(serviceJob.Id);
        result.Content.Should().Be(content);
        result.Type.Should().Be(noteType);
        result.IsCustomerVisible.Should().Be(isCustomerVisible);
        result.IsSensitive.Should().Be(isSensitive);
        result.AuthorUserId.Should().Be(authorUserId);
        result.AuthorName.Should().Be(authorName);
        result.TenantId.Should().Be(_testTenantId);
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_ShouldThrowArgumentException_WhenContentInvalid(string invalidContent)
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);
        await _context.SaveChangesAsync();

        var noteType = JobNoteType.Technical;
        var authorUserId = Guid.NewGuid();
        var authorName = "Test User";

        // Act & Assert
        await FluentActions.Invoking(() => _jobNoteService.CreateAsync(
                invalidContent,
                noteType,
                serviceJob.Id,
                authorUserId,
                authorName))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNote_WhenNoteExists()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        await _context.JobNotes.AddAsync(jobNote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobNoteService.GetByIdAsync(jobNote.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(jobNote.Id);
        result.Content.Should().Be(jobNote.Content);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNoteNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobNoteService.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByServiceJobAsync_ShouldReturnNotesForJob_WhenJobExists()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var note1 = CreateTestJobNote(serviceJob.Id, "First note");
        var note2 = CreateTestJobNote(serviceJob.Id, "Second note");
        var note3 = CreateTestJobNote(Guid.NewGuid(), "Other job note"); // Different job

        await _context.JobNotes.AddRangeAsync(note1, note2, note3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobNoteService.GetByServiceJobAsync(serviceJob.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(n => n.Content == "First note");
        result.Should().Contain(n => n.Content == "Second note");
        result.Should().NotContain(n => n.Content == "Other job note");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateNote_WhenValidData()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        await _context.JobNotes.AddAsync(jobNote);
        await _context.SaveChangesAsync();

        var newContent = "Updated note content";
        var isCustomerVisible = false; // Technical notes start as non-customer visible
        var isSensitive = true;  // Making it sensitive
        var updatedByUserId = Guid.NewGuid();
        var updatedByUserName = "Updater User";

        // Act
        var result = await _jobNoteService.UpdateAsync(
            jobNote.Id,
            newContent,
            jobNote.Type, // Type cannot be changed after creation
            isCustomerVisible,
            isSensitive,
            updatedByUserId,
            updatedByUserName);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Be(newContent);
        result.Type.Should().Be(jobNote.Type); // Type remains unchanged
        result.IsCustomerVisible.Should().BeFalse(); // Sensitive notes are hidden from customers
        result.IsSensitive.Should().BeTrue();
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SoftDeleteAsync_ShouldMarkNoteAsDeleted_WhenNoteExists()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        await _context.JobNotes.AddAsync(jobNote);
        await _context.SaveChangesAsync();

        var deletedByUserId = Guid.NewGuid();
        var deletedByUserName = "Deleter User";
        var reason = "Test deletion";

        // Act
        await _jobNoteService.SoftDeleteAsync(
            jobNote.Id,
            deletedByUserId,
            deletedByUserName,
            reason);

        // Assert
        var deletedNote = await _context.JobNotes.FindAsync(jobNote.Id);
        deletedNote.Should().NotBeNull();
        deletedNote!.IsDeleted.Should().BeTrue();
        deletedNote.DeletedAt.Should().NotBeNull();
        deletedNote.DeletedByUserId.Should().Be(deletedByUserId);
        deletedNote.DeletedByUserName.Should().Be(deletedByUserName);
        deletedNote.DeletionReason.Should().Be(reason);
    }

    [Fact]
    public async Task FilterAsync_ShouldFilterByType_WhenTypeSpecified()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var technicalNote = CreateTestJobNote(serviceJob.Id, "Technical note", noteType: JobNoteType.Technical);
        var internalNote = CreateTestJobNote(serviceJob.Id, "Internal note", noteType: JobNoteType.Internal);

        await _context.JobNotes.AddRangeAsync(technicalNote, internalNote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobNoteService.FilterAsync(type: JobNoteType.Technical);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(n => n.Content == "Technical note");
        result.Should().NotContain(n => n.Type != JobNoteType.Technical);
    }

    [Fact]
    public async Task GetStatsAsync_ShouldReturnCorrectStats_WhenNotesExist()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var customerVisibleNote = CreateTestJobNote(serviceJob.Id, "Customer note", isCustomerVisible: true);
        var sensitiveNote = CreateTestJobNote(serviceJob.Id, "Sensitive note", isSensitive: true);
        var regularNote = CreateTestJobNote(serviceJob.Id, "Regular note");

        await _context.JobNotes.AddRangeAsync(customerVisibleNote, sensitiveNote, regularNote);
        await _context.SaveChangesAsync();

        // Act
        var stats = await _jobNoteService.GetStatsAsync(serviceJob.Id);

        // Assert
        stats.Total.Should().Be(3);
        stats.CustomerVisible.Should().Be(1);
        stats.Sensitive.Should().Be(1);
        stats.Deleted.Should().Be(0);
    }

    private ServiceJob CreateTestServiceJob()
    {
        return new ServiceJob(
            "JOB20241201-001",
            "Test Customer",
            new Domain.ValueObjects.Address(
                "123 Test St",
                "Test City", 
                "TS",
                "12345",
                "USA",
                "123 Test St, Test City, TS 12345",
                new Domain.ValueObjects.Coordinate(40.7128, -74.0060)
            ),
            "Test service job description",
            DateTime.Today.AddDays(1),
            TimeSpan.FromHours(2),
            _testTenantId
        );
    }

    private JobNote CreateTestJobNote(Guid? serviceJobId = null, string content = "Test note", 
        JobNoteType noteType = JobNoteType.Technical, bool isCustomerVisible = false, bool isSensitive = false)
    {
        return new JobNote(
            content,
            noteType,
            serviceJobId ?? Guid.NewGuid(),
            _testTenantId,
            Guid.NewGuid(),
            "Test Creator",
            isCustomerVisible: isCustomerVisible,
            isSensitive: isSensitive
        );
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}