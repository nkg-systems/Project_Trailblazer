using AutoFixture.Xunit2;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Tests.TestBase;
using FieldOpsOptimizer.Domain.ValueObjects;
using FluentAssertions;

namespace FieldOpsOptimizer.Domain.Tests.Entities;

public class ServiceJobTests : DomainTestBase
{
    [Fact]
    public void ServiceJob_Constructor_ShouldInitializeWithValidData()
    {
        // Arrange
        var jobNumber = "JOB20241201-001";
        var customerName = "John Doe";
        var address = new Address("123 Main St", "Anytown", "CA", "12345", "USA", "123 Main St, Anytown, CA 12345", new Coordinate(34.0522, -118.2437));
        var description = "Fix plumbing issue";
        var scheduledDate = DateTime.Today.AddDays(1);
        var estimatedDuration = TimeSpan.FromHours(2);
        var tenantId = "test-tenant";
        var priority = JobPriority.High;

        // Act
        var job = new ServiceJob(jobNumber, customerName, address, description, scheduledDate, estimatedDuration, tenantId, priority);

        // Assert
        job.JobNumber.Should().Be(jobNumber);
        job.CustomerName.Should().Be(customerName);
        job.ServiceAddress.Should().Be(address);
        job.Description.Should().Be(description);
        job.ScheduledDate.Should().Be(scheduledDate);
        job.EstimatedDuration.Should().Be(estimatedDuration);
        job.TenantId.Should().Be(tenantId);
        job.Priority.Should().Be(priority);
        job.Status.Should().Be(JobStatus.Scheduled);
        job.AssignedTechnicianId.Should().BeNull();
        job.RouteId.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.RequiredSkills.Should().BeEmpty();
        job.Tags.Should().BeEmpty();
    }

    [Fact]
    public void UpdateCustomerInfo_ShouldUpdateCustomerDetails()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var newCustomerName = "Jane Smith";
        var newPhone = "555-1234";
        var newEmail = "jane@example.com";
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.UpdateCustomerInfo(newCustomerName, newPhone, newEmail);

        // Assert
        job.CustomerName.Should().Be(newCustomerName);
        job.CustomerPhone.Should().Be(newPhone);
        job.CustomerEmail.Should().Be(newEmail);
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdateServiceDetails_ShouldUpdateDescriptionAndNotes()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var newDescription = "Updated repair description";
        var newNotes = "Customer prefers morning appointment";
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.UpdateServiceDetails(newDescription, newNotes);

        // Assert
        job.Description.Should().Be(newDescription);
        job.Notes.Should().Be(newNotes);
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdateSchedule_ShouldUpdateScheduledDateAndTimeWindow()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var newScheduledDate = DateTime.Today.AddDays(3);
        var newTimeWindow = TimeSpan.FromHours(4);
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.UpdateSchedule(newScheduledDate, newTimeWindow);

        // Assert
        job.ScheduledDate.Should().Be(newScheduledDate);
        job.PreferredTimeWindow.Should().Be(newTimeWindow);
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void AssignTechnician_ShouldSetTechnicianId()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var technicianId = Guid.NewGuid();
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.AssignTechnician(technicianId);

        // Assert
        job.AssignedTechnicianId.Should().Be(technicianId);
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UnassignTechnician_ShouldClearTechnicianId()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var technicianId = Guid.NewGuid();
        job.AssignTechnician(technicianId);
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.UnassignTechnician();

        // Assert
        job.AssignedTechnicianId.Should().BeNull();
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void AssignToRoute_ShouldSetRouteId()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var routeId = Guid.NewGuid();
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.AssignToRoute(routeId);

        // Assert
        job.RouteId.Should().Be(routeId);
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Theory]
    [InlineData(JobStatus.InProgress)]
    [InlineData(JobStatus.Cancelled)]
    public void UpdateStatus_ShouldChangeStatus(JobStatus newStatus)
    {
        // Arrange
        var job = CreateValidServiceJob();
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.UpdateStatus(newStatus);

        // Assert
        job.Status.Should().Be(newStatus);
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdateStatus_WhenCompleted_ShouldSetCompletedAt()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var beforeCompletion = DateTime.UtcNow;

        // Act
        job.UpdateStatus(JobStatus.Completed);

        // Assert
        job.Status.Should().Be(JobStatus.Completed);
        job.CompletedAt.Should().NotBeNull();
        job.CompletedAt.Should().BeOnOrAfter(beforeCompletion);
    }

    [Theory]
    [InlineData(JobPriority.Low)]
    [InlineData(JobPriority.Medium)]
    [InlineData(JobPriority.High)]
    [InlineData(JobPriority.Emergency)]
    public void UpdatePriority_ShouldChangePriority(JobPriority newPriority)
    {
        // Arrange
        var job = CreateValidServiceJob();
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.UpdatePriority(newPriority);

        // Assert
        job.Priority.Should().Be(newPriority);
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void AddRequiredSkill_ShouldAddSkillToList()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var skill = "HVAC";
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.AddRequiredSkill(skill);

        // Assert
        job.RequiredSkills.Should().Contain(skill);
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void AddRequiredSkill_WhenSkillAlreadyExists_ShouldNotDuplicate()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var skill = "Electrical";
        job.AddRequiredSkill(skill);
        var skillCountBefore = job.RequiredSkills.Count;

        // Act
        job.AddRequiredSkill(skill); // Try to add same skill again

        // Assert
        job.RequiredSkills.Count.Should().Be(skillCountBefore);
        job.RequiredSkills.Should().ContainSingle(s => s == skill);
    }

    [Fact]
    public void AddRequiredSkill_ShouldBeCaseInsensitive()
    {
        // Arrange
        var job = CreateValidServiceJob();
        job.AddRequiredSkill("electrical");
        var skillCountBefore = job.RequiredSkills.Count;

        // Act
        job.AddRequiredSkill("ELECTRICAL");

        // Assert
        job.RequiredSkills.Count.Should().Be(skillCountBefore);
    }

    [Fact]
    public void RemoveRequiredSkill_ShouldRemoveSkillFromList()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var skill = "Plumbing";
        job.AddRequiredSkill(skill);

        // Act
        job.RemoveRequiredSkill(skill);

        // Assert
        job.RequiredSkills.Should().NotContain(skill);
    }

    [Fact]
    public void RemoveRequiredSkill_ShouldBeCaseInsensitive()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var skill = "Plumbing";
        job.AddRequiredSkill(skill);

        // Act
        job.RemoveRequiredSkill("PLUMBING");

        // Assert
        job.RequiredSkills.Should().NotContain(skill);
    }

    [Fact]
    public void AddTag_ShouldAddTagToList()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var tag = "Urgent";
        var originalUpdatedAt = job.UpdatedAt;

        // Act
        job.AddTag(tag);

        // Assert
        job.Tags.Should().Contain(tag);
        job.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void AddTag_WhenTagAlreadyExists_ShouldNotDuplicate()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var tag = "Urgent";
        job.AddTag(tag);
        var tagCountBefore = job.Tags.Count;

        // Act
        job.AddTag(tag); // Try to add same tag again

        // Assert
        job.Tags.Count.Should().Be(tagCountBefore);
        job.Tags.Should().ContainSingle(t => t == tag);
    }

    [Fact]
    public void AddTag_ShouldBeCaseInsensitive()
    {
        // Arrange
        var job = CreateValidServiceJob();
        job.AddTag("urgent");
        var tagCountBefore = job.Tags.Count;

        // Act
        job.AddTag("URGENT");

        // Assert
        job.Tags.Count.Should().Be(tagCountBefore);
    }

    [Fact]
    public void RemoveTag_ShouldRemoveTagFromList()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var tag = "Urgent";
        job.AddTag(tag);

        // Act
        job.RemoveTag(tag);

        // Assert
        job.Tags.Should().NotContain(tag);
    }

    [Fact]
    public void RemoveTag_ShouldBeCaseInsensitive()
    {
        // Arrange
        var job = CreateValidServiceJob();
        var tag = "Urgent";
        job.AddTag(tag);

        // Act
        job.RemoveTag("URGENT");

        // Assert
        job.Tags.Should().NotContain(tag);
    }

    [Fact]
    public void RequiresSkills_WhenTechnicianHasAllSkills_ShouldReturnTrue()
    {
        // Arrange
        var job = CreateValidServiceJob();
        job.AddRequiredSkill("Electrical");
        job.AddRequiredSkill("Plumbing");
        
        var technicianSkills = new[] { "Electrical", "Plumbing", "HVAC" };

        // Act
        var result = job.RequiresSkills(technicianSkills);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RequiresSkills_WhenTechnicianMissingSkills_ShouldReturnFalse()
    {
        // Arrange
        var job = CreateValidServiceJob();
        job.AddRequiredSkill("Electrical");
        job.AddRequiredSkill("Plumbing");
        job.AddRequiredSkill("HVAC");
        
        var technicianSkills = new[] { "Electrical", "Plumbing" };

        // Act
        var result = job.RequiresSkills(technicianSkills);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RequiresSkills_ShouldBeCaseInsensitive()
    {
        // Arrange
        var job = CreateValidServiceJob();
        job.AddRequiredSkill("Electrical");
        job.AddRequiredSkill("Plumbing");
        
        var technicianSkills = new[] { "electrical", "PLUMBING" };

        // Act
        var result = job.RequiresSkills(technicianSkills);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RequiresSkills_WhenNoRequiredSkills_ShouldReturnTrue()
    {
        // Arrange
        var job = CreateValidServiceJob();
        // Don't add any required skills
        
        var technicianSkills = new[] { "Electrical" };

        // Act
        var result = job.RequiresSkills(technicianSkills);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetDistanceToInKilometers_ShouldCalculateDistanceCorrectly()
    {
        // Arrange
        var coordinate1 = new Coordinate(34.0522, -118.2437); // Los Angeles
        var coordinate2 = new Coordinate(40.7128, -74.0060);  // New York
        
        var address = new Address("123 Main St", "Los Angeles", "CA", "90210", "USA", "123 Main St, Los Angeles, CA 90210", coordinate1);
        var job = new ServiceJob("JOB20241201-001", "Test Customer", address, "Test job", DateTime.Today.AddDays(1), TimeSpan.FromHours(2), "test-tenant");

        // Act
        var distance = job.GetDistanceToInKilometers(coordinate2);

        // Assert
        distance.Should().BeGreaterThan(0);
        distance.Should().BeApproximately(3944, 50); // Approximate distance between LA and NYC in km
    }

    [Fact]
    public void GetDistanceToInKilometers_WhenNoCoordinate_ShouldReturnMaxValue()
    {
        // Arrange
        var address = new Address("123 Main St", "Los Angeles", "CA", "90210", "USA", "123 Main St, Los Angeles, CA 90210", null);
        var job = new ServiceJob("JOB20241201-001", "Test Customer", address, "Test job", DateTime.Today.AddDays(1), TimeSpan.FromHours(2), "test-tenant");
        var targetCoordinate = new Coordinate(40.7128, -74.0060);

        // Act
        var distance = job.GetDistanceToInKilometers(targetCoordinate);

        // Assert
        distance.Should().Be(double.MaxValue);
    }

    [Theory]
    [DomainAutoData]
    public void ServiceJob_PropertyChanges_ShouldUpdateTimestamp(
        string jobNumber,
        string customerName, 
        Address address,
        string description,
        DateTime scheduledDate,
        TimeSpan estimatedDuration,
        string tenantId)
    {
        // Arrange
        var job = new ServiceJob(jobNumber, customerName, address, description, scheduledDate, estimatedDuration, tenantId);
        var originalTimestamp = job.UpdatedAt;

        // Act & Assert - Test multiple operations update the timestamp
        job.UpdateCustomerInfo("New Customer");
        job.UpdatedAt.Should().BeAfter(originalTimestamp);

        var timestamp2 = job.UpdatedAt;
        job.UpdateServiceDetails("New description");
        job.UpdatedAt.Should().BeAfter(timestamp2);

        var timestamp3 = job.UpdatedAt;
        job.AddRequiredSkill("New Skill");
        job.UpdatedAt.Should().BeAfter(timestamp3);
    }
}
