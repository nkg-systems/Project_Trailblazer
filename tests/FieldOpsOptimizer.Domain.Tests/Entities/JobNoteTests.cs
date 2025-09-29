using Xunit;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Domain.Tests.Entities;

public class JobNoteTests
{
    private readonly Guid _serviceJobId = Guid.NewGuid();
    private readonly Guid _authorUserId = Guid.NewGuid();
    private const string TenantId = "test-tenant";
    private const string AuthorName = "John Doe";

    [Fact]
    public void Constructor_WithValidParameters_CreatesJobNote()
    {
        // Arrange
        const string content = "This is a test note";
        const JobNoteType type = JobNoteType.General;

        // Act
        var jobNote = new JobNote(
            content,
            type,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName);

        // Assert
        Assert.Equal(content, jobNote.Content);
        Assert.Equal(type, jobNote.Type);
        Assert.Equal(_serviceJobId, jobNote.ServiceJobId);
        Assert.Equal(TenantId, jobNote.TenantId);
        Assert.Equal(_authorUserId, jobNote.AuthorUserId);
        Assert.Equal(AuthorName, jobNote.AuthorName);
        Assert.False(jobNote.IsCustomerVisible);
        Assert.False(jobNote.IsSensitive);
        Assert.False(jobNote.IsDeleted);
    }

    [Fact]
    public void Constructor_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new JobNote(
            string.Empty,
            JobNoteType.General,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName));
    }

    [Fact]
    public void Constructor_WithEmptyServiceJobId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new JobNote(
            "Valid content",
            JobNoteType.General,
            Guid.Empty,
            TenantId,
            _authorUserId,
            AuthorName));
    }

    [Fact]
    public void Constructor_WithEmptyAuthorUserId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new JobNote(
            "Valid content",
            JobNoteType.General,
            _serviceJobId,
            TenantId,
            Guid.Empty,
            AuthorName));
    }

    [Fact]
    public void Constructor_WithEmptyTenantId_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new JobNote(
            "Valid content",
            JobNoteType.General,
            _serviceJobId,
            string.Empty,
            _authorUserId,
            AuthorName));
    }

    [Fact]
    public void Constructor_WithCustomerCommunicationType_SetsCustomerVisibleToTrue()
    {
        // Arrange & Act
        var jobNote = new JobNote(
            "Customer communication",
            JobNoteType.CustomerCommunication,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName);

        // Assert
        Assert.True(jobNote.IsCustomerVisible);
    }

    [Fact]
    public void UpdateContent_WithValidData_UpdatesContent()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        const string newContent = "Updated content";
        var updatedByUserId = Guid.NewGuid();
        const string updatedByUserName = "Jane Doe";

        // Act
        jobNote.UpdateContent(newContent, updatedByUserId, updatedByUserName);

        // Assert
        Assert.Equal(newContent, jobNote.Content);
    }

    [Fact]
    public void UpdateContent_OnDeletedNote_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        jobNote.SoftDelete(Guid.NewGuid(), "Admin", "Test deletion");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            jobNote.UpdateContent("New content", Guid.NewGuid(), "User"));
    }

    [Fact]
    public void SetCustomerVisibility_OnInternalNote_AllowsVisibilityChange()
    {
        // Arrange
        var jobNote = new JobNote(
            "Internal note",
            JobNoteType.Internal,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName);

        // Act
        jobNote.SetCustomerVisibility(false, Guid.NewGuid()); // This should work

        // Assert
        Assert.False(jobNote.IsCustomerVisible);
    }

    [Fact]
    public void SetCustomerVisibility_MakingInternalNoteCustomerVisible_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobNote = new JobNote(
            "Internal note",
            JobNoteType.Internal,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            jobNote.SetCustomerVisibility(true, Guid.NewGuid()));
    }

    [Fact]
    public void SetCustomerVisibility_HidingCustomerCommunicationNote_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobNote = new JobNote(
            "Customer communication",
            JobNoteType.CustomerCommunication,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            jobNote.SetCustomerVisibility(false, Guid.NewGuid()));
    }

    [Fact]
    public void SetSensitiveFlag_WithNonCustomerCommunication_HidesFromCustomer()
    {
        // Arrange
        var jobNote = new JobNote(
            "General note",
            JobNoteType.General,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName,
            isCustomerVisible: true);

        // Act
        jobNote.SetSensitiveFlag(true, Guid.NewGuid());

        // Assert
        Assert.True(jobNote.IsSensitive);
        Assert.False(jobNote.IsCustomerVisible);
    }

    [Fact]
    public void SoftDelete_WithValidData_MarksAsDeleted()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        var deletedByUserId = Guid.NewGuid();
        const string deletedByUserName = "Admin";
        const string reason = "Test deletion";

        // Act
        jobNote.SoftDelete(deletedByUserId, deletedByUserName, reason);

        // Assert
        Assert.True(jobNote.IsDeleted);
        Assert.NotNull(jobNote.DeletedAt);
        Assert.Equal(deletedByUserId, jobNote.DeletedByUserId);
        Assert.Equal(deletedByUserName, jobNote.DeletedByUserName);
        Assert.Equal(reason, jobNote.DeletionReason);
    }

    [Fact]
    public void SoftDelete_OnAlreadyDeletedNote_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        jobNote.SoftDelete(Guid.NewGuid(), "Admin", "First deletion");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            jobNote.SoftDelete(Guid.NewGuid(), "Another Admin", "Second deletion"));
    }

    [Fact]
    public void Restore_OnDeletedNote_RestoresNote()
    {
        // Arrange
        var jobNote = CreateTestJobNote();
        jobNote.SoftDelete(Guid.NewGuid(), "Admin", "Test deletion");
        var restoredByUserId = Guid.NewGuid();

        // Act
        jobNote.Restore(restoredByUserId);

        // Assert
        Assert.False(jobNote.IsDeleted);
        Assert.Null(jobNote.DeletedAt);
        Assert.Null(jobNote.DeletedByUserId);
        Assert.Null(jobNote.DeletedByUserName);
        Assert.Null(jobNote.DeletionReason);
    }

    [Fact]
    public void Restore_OnNonDeletedNote_ThrowsInvalidOperationException()
    {
        // Arrange
        var jobNote = CreateTestJobNote();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            jobNote.Restore(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(true, true, false, true)]   // Internal permission, sensitive permission, not customer -> can view
    [InlineData(false, true, false, false)] // No internal permission, sensitive permission, not customer -> cannot view
    [InlineData(true, false, false, false)] // Internal permission, no sensitive permission, not customer -> cannot view (sensitive note)
    [InlineData(true, true, true, false)]   // Internal permission, sensitive permission, is customer -> cannot view (customer can't see sensitive)
    public void CanBeViewedBy_WithDifferentPermissions_ReturnsCorrectResult(
        bool canViewInternal, bool canViewSensitive, bool isCustomer, bool expectedResult)
    {
        // Arrange
        var jobNote = new JobNote(
            "Sensitive internal note",
            JobNoteType.Internal,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName,
            isSensitive: true);

        // Act
        var canView = jobNote.CanBeViewedBy(canViewInternal, canViewSensitive, isCustomer);

        // Assert
        Assert.Equal(expectedResult, canView);
    }

    [Fact]
    public void GetLogSafeContent_WithSensitiveNote_ReturnsMaskedContent()
    {
        // Arrange
        var jobNote = new JobNote(
            "Sensitive information here",
            JobNoteType.Internal,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName,
            isSensitive: true);

        // Act
        var logSafeContent = jobNote.GetLogSafeContent();

        // Assert
        Assert.Equal("[SENSITIVE CONTENT]", logSafeContent);
    }

    [Fact]
    public void GetLogSafeContent_WithLongContent_TruncatesContent()
    {
        // Arrange
        var longContent = new string('x', 200);
        var jobNote = new JobNote(
            longContent,
            JobNoteType.General,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName);

        // Act
        var logSafeContent = jobNote.GetLogSafeContent();

        // Assert
        Assert.True(logSafeContent.Length <= 100);
        Assert.EndsWith("...", logSafeContent);
    }

    private JobNote CreateTestJobNote()
    {
        return new JobNote(
            "Test content",
            JobNoteType.General,
            _serviceJobId,
            TenantId,
            _authorUserId,
            AuthorName);
    }
}