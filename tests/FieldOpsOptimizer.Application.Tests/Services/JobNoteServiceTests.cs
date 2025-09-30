using AutoFixture;
using AutoFixture.Xunit2;
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

public class JobNoteServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<JobNoteService>> _mockLogger;
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
        _mockLogger = new Mock<ILogger<JobNoteService>>();
        _mockTenantService = new Mock<ITenantService>();
        
        _mockTenantService.Setup(x => x.GetCurrentTenantId())
            .Returns(_testTenantId);
        
        _jobNoteService = new JobNoteService(_context, _mockLogger.Object, _mockTenantService.Object);
        
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Fact]
    public async Task CreateNoteAsync_ShouldCreateJobNote_WhenValidData()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);
        await _context.SaveChangesAsync();

        var noteText = "This is a test note";
        var noteType = JobNoteType.General;
        var isPrivate = false;
        var createdByUserId = Guid.NewGuid();
        var createdByUserName = "Test User";

        // Act
        var result = await _jobNoteService.CreateNoteAsync(
            serviceJob.Id, 
            noteText, 
            noteType, 
            isPrivate, 
            createdByUserId, 
            createdByUserName);

        // Assert
        result.Should().NotBeNull();
        result.JobId.Should().Be(serviceJob.Id);
        result.NoteText.Should().Be(noteText);
        result.NoteType.Should().Be(noteType);
        result.IsPrivate.Should().Be(isPrivate);
        result.CreatedByUserId.Should().Be(createdByUserId);
        result.CreatedByUserName.Should().Be(createdByUserName);
        result.TenantId.Should().Be(_testTenantId);
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateNoteAsync_ShouldThrowArgumentException_WhenJobNotFound()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid();
        var noteText = "This is a test note";
        var noteType = JobNoteType.General;
        var createdByUserId = Guid.NewGuid();
        var createdByUserName = "Test User";

        // Act & Assert
        await FluentActions.Invoking(() => _jobNoteService.CreateNoteAsync(
                nonExistentJobId, 
                noteText, 
                noteType, 
                false, 
                createdByUserId, 
                createdByUserName))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage($"Service job with ID {nonExistentJobId} not found in tenant {_testTenantId}*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateNoteAsync_ShouldThrowArgumentException_WhenNoteTextInvalid(string invalidNoteText)
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);
        await _context.SaveChangesAsync();

        var noteType = JobNoteType.General;
        var createdByUserId = Guid.NewGuid();
        var createdByUserName = "Test User";

        // Act & Assert
        await FluentActions.Invoking(() => _jobNoteService.CreateNoteAsync(
                serviceJob.Id, 
                invalidNoteText, 
                noteType, 
                false, 
                createdByUserId, 
                createdByUserName))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("Note text cannot be null or empty*");
    }

    [Fact]
    public async Task GetNoteByIdAsync_ShouldReturnNote_WhenNoteExists()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        await _context.JobNotes.AddAsync(jobNote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobNoteService.GetNoteByIdAsync(jobNote.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(jobNote.Id);
        result.NoteText.Should().Be(jobNote.NoteText);
    }

    [Fact]
    public async Task GetNoteByIdAsync_ShouldReturnNull_WhenNoteNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobNoteService.GetNoteByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNotesByJobIdAsync_ShouldReturnNotesForJob_WhenJobExists()
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
        var result = await _jobNoteService.GetNotesByJobIdAsync(serviceJob.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(n => n.NoteText == "First note");
        result.Should().Contain(n => n.NoteText == "Second note");
        result.Should().NotContain(n => n.NoteText == "Other job note");
    }

    [Fact]
    public async Task GetNotesByJobIdAsync_ShouldFilterPrivateNotes_WhenRequesterIsNotCreator()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var publicNote = CreateTestJobNote(serviceJob.Id, "Public note", isPrivate: false);
        var privateNote = CreateTestJobNote(serviceJob.Id, "Private note", isPrivate: true);

        await _context.JobNotes.AddRangeAsync(publicNote, privateNote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobNoteService.GetNotesByJobIdAsync(serviceJob.Id, 
            includePrivate: false, 
            requestingUserId: Guid.NewGuid()); // Different user

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(n => n.NoteText == "Public note");
        result.Should().NotContain(n => n.NoteText == "Private note");
    }

    [Fact]
    public async Task UpdateNoteAsync_ShouldUpdateNote_WhenValidData()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        await _context.JobNotes.AddAsync(jobNote);
        await _context.SaveChangesAsync();

        var newNoteText = "Updated note text";
        var newNoteType = JobNoteType.Technical;
        var newIsPrivate = !jobNote.IsPrivate;
        var updatedByUserId = Guid.NewGuid();
        var updatedByUserName = "Updater User";

        // Act
        var result = await _jobNoteService.UpdateNoteAsync(
            jobNote.Id, 
            newNoteText, 
            newNoteType, 
            newIsPrivate, 
            updatedByUserId, 
            updatedByUserName);

        // Assert
        result.Should().NotBeNull();
        result!.NoteText.Should().Be(newNoteText);
        result.NoteType.Should().Be(newNoteType);
        result.IsPrivate.Should().Be(newIsPrivate);
        result.LastUpdatedByUserId.Should().Be(updatedByUserId);
        result.LastUpdatedByUserName.Should().Be(updatedByUserName);
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateNoteAsync_ShouldReturnNull_WhenNoteNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var newNoteText = "Updated note text";
        var newNoteType = JobNoteType.Technical;
        var updatedByUserId = Guid.NewGuid();
        var updatedByUserName = "Updater User";

        // Act
        var result = await _jobNoteService.UpdateNoteAsync(
            nonExistentId, 
            newNoteText, 
            newNoteType, 
            false, 
            updatedByUserId, 
            updatedByUserName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNoteAsync_ShouldReturnTrue_WhenNoteExists()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        await _context.JobNotes.AddAsync(jobNote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobNoteService.DeleteNoteAsync(jobNote.Id);

        // Assert
        result.Should().BeTrue();
        
        // Verify note is deleted
        var deletedNote = await _context.JobNotes.FindAsync(jobNote.Id);
        deletedNote.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNoteAsync_ShouldReturnFalse_WhenNoteNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _jobNoteService.DeleteNoteAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetNotesByTypeAsync_ShouldFilterByType_WhenTypeSpecified()
    {
        // Arrange
        var serviceJob = CreateTestServiceJob();
        await _context.ServiceJobs.AddAsync(serviceJob);

        var generalNote = CreateTestJobNote(serviceJob.Id, "General note", noteType: JobNoteType.General);
        var technicalNote = CreateTestJobNote(serviceJob.Id, "Technical note", noteType: JobNoteType.Technical);
        var internalNote = CreateTestJobNote(serviceJob.Id, "Internal note", noteType: JobNoteType.Internal);

        await _context.JobNotes.AddRangeAsync(generalNote, technicalNote, internalNote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _jobNoteService.GetNotesByTypeAsync(JobNoteType.Technical);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().Contain(n => n.NoteText == "Technical note");
        result.Should().NotContain(n => n.NoteType != JobNoteType.Technical);
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

    private JobNote CreateTestJobNote(Guid? jobId = null, string noteText = "Test note", 
        JobNoteType noteType = JobNoteType.General, bool isPrivate = false)
    {
        return new JobNote(
            jobId ?? Guid.NewGuid(),
            noteText,
            noteType,
            isPrivate,
            Guid.NewGuid(),
            "Test Creator",
            _testTenantId
        );
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}