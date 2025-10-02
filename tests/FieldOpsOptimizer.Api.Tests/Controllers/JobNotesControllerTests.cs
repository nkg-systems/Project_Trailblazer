using FieldOpsOptimizer.Api.Tests.TestBase;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace FieldOpsOptimizer.Api.Tests.Controllers;

public class JobNotesControllerTests : ApiIntegrationTestBase
{
    private ServiceJob _testServiceJob = null!;
    private const string TestTenantId = "test-tenant";

    public JobNotesControllerTests(CustomWebApplicationFactory<Program> factory) : base(factory)
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
    public async Task GetJobNotes_ShouldReturnNotes_WhenJobHasNotes()
    {
        // Arrange
        var note1 = new JobNote(
            "Technical note 1",
            JobNoteType.Technical,
            _testServiceJob.Id,
            TestTenantId,
            Guid.NewGuid(),
            "Test User");

        var note2 = new JobNote(
            "Internal note 1",
            JobNoteType.Internal,
            _testServiceJob.Id,
            TestTenantId,
            Guid.NewGuid(),
            "Test User 2");

        Context.JobNotes.AddRange(note1, note2);
        await Context.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/jobnotes?serviceJobId={_testServiceJob.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var notes = JsonSerializer.Deserialize<List<JobNoteDto>>(content, JsonOptions);
        
        notes.Should().HaveCount(2);
        notes.Should().Contain(n => n.Content == "Technical note 1");
        notes.Should().Contain(n => n.Content == "Internal note 1");
    }

    [Fact]
    public async Task CreateJobNote_ShouldReturnCreatedNote_WhenDataIsValid()
    {
        // Arrange
        var request = new CreateJobNoteRequest
        {
            Content = "New technical note",
            Type = JobNoteType.Technical,
            ServiceJobId = _testServiceJob.Id,
            IsCustomerVisible = false,
            IsSensitive = false
        };

        // Act
        var response = await Client.PostAsync("/api/jobnotes", CreateJsonContent(request));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var createdNote = JsonSerializer.Deserialize<JobNoteDto>(content, JsonOptions);

        createdNote.Should().NotBeNull();
        createdNote!.Content.Should().Be("New technical note");
        createdNote.Type.Should().Be(JobNoteType.Technical);
        createdNote.ServiceJobId.Should().Be(_testServiceJob.Id);
        createdNote.IsCustomerVisible.Should().BeFalse();
        createdNote.IsSensitive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateJobNote_ShouldReturnUpdatedNote_WhenDataIsValid()
    {
        // Arrange
        var note = new JobNote(
            "Original content",
            JobNoteType.Technical,
            _testServiceJob.Id,
            TestTenantId,
            Guid.NewGuid(),
            "Test User");

        Context.JobNotes.Add(note);
        await Context.SaveChangesAsync();

        var request = new UpdateJobNoteRequest
        {
            Content = "Updated content",
            IsCustomerVisible = false,
            IsSensitive = true
        };

        // Act
        var response = await Client.PutAsync($"/api/jobnotes/{note.Id}", CreateJsonContent(request));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var updatedNote = JsonSerializer.Deserialize<JobNoteDto>(content, JsonOptions);

        updatedNote.Should().NotBeNull();
        updatedNote!.Content.Should().Be("Updated content");
        updatedNote.IsSensitive.Should().BeTrue();
        updatedNote.IsCustomerVisible.Should().BeFalse(); // Should be false when marked as sensitive
    }

    [Fact]
    public async Task DeleteJobNote_ShouldReturnNoContent_WhenNoteExists()
    {
        // Arrange
        var note = new JobNote(
            "Note to delete",
            JobNoteType.Technical,
            _testServiceJob.Id,
            TestTenantId,
            Guid.NewGuid(),
            "Test User");

        Context.JobNotes.Add(note);
        await Context.SaveChangesAsync();

        // Act
        var response = await Client.DeleteAsync($"/api/jobnotes/{note.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify note is soft deleted
        var deletedNote = await Context.JobNotes.FindAsync(note.Id);
        deletedNote.Should().NotBeNull();
        deletedNote!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetJobNoteStats_ShouldReturnCorrectStats_WhenJobHasNotes()
    {
        // Arrange
        var customerNote = new JobNote(
            "Customer visible note",
            JobNoteType.CustomerCommunication,
            _testServiceJob.Id,
            TestTenantId,
            Guid.NewGuid(),
            "Test User");

        var sensitiveNote = new JobNote(
            "Sensitive note",
            JobNoteType.Technical,
            _testServiceJob.Id,
            TestTenantId,
            Guid.NewGuid(),
            "Test User",
            isSensitive: true);

        var regularNote = new JobNote(
            "Regular note",
            JobNoteType.Internal,
            _testServiceJob.Id,
            TestTenantId,
            Guid.NewGuid(),
            "Test User");

        Context.JobNotes.AddRange(customerNote, sensitiveNote, regularNote);
        await Context.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync($"/api/jobnotes/{_testServiceJob.Id}/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<JobNoteStatsDto>(content, JsonOptions);

        stats.Should().NotBeNull();
        stats!.Total.Should().Be(3);
        stats.CustomerVisible.Should().Be(1);
        stats.Sensitive.Should().Be(1);
        stats.Deleted.Should().Be(0);
    }

    protected override async Task CleanupTestDataAsync()
    {
        Context.JobNotes.RemoveRange(Context.JobNotes);
        Context.ServiceJobs.RemoveRange(Context.ServiceJobs);
        await Context.SaveChangesAsync();
    }

    private static System.Net.Http.StringContent CreateJsonContent(object obj)
    {
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    // DTOs for API responses - these should match the actual API DTOs
    public class JobNoteDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public JobNoteType Type { get; set; }
        public Guid ServiceJobId { get; set; }
        public bool IsCustomerVisible { get; set; }
        public bool IsSensitive { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class CreateJobNoteRequest
    {
        public string Content { get; set; } = string.Empty;
        public JobNoteType Type { get; set; }
        public Guid ServiceJobId { get; set; }
        public bool IsCustomerVisible { get; set; }
        public bool IsSensitive { get; set; }
    }

    public class UpdateJobNoteRequest
    {
        public string Content { get; set; } = string.Empty;
        public bool IsCustomerVisible { get; set; }
        public bool IsSensitive { get; set; }
    }

    public class JobNoteStatsDto
    {
        public int Total { get; set; }
        public int CustomerVisible { get; set; }
        public int Sensitive { get; set; }
        public int Deleted { get; set; }
    }
}