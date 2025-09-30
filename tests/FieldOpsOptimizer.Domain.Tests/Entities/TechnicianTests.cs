using AutoFixture.Xunit2;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.Tests.TestBase;
using FieldOpsOptimizer.Domain.ValueObjects;
using FluentAssertions;

namespace FieldOpsOptimizer.Domain.Tests.Entities;

public class TechnicianTests : DomainTestBase
{
    [Fact]
    public void Technician_Constructor_ShouldInitializeWithValidData()
    {
        // Arrange
        var employeeId = "EMP001";
        var firstName = "John";
        var lastName = "Smith";
        var email = "john.smith@company.com";
        var tenantId = "test-tenant";
        var hourlyRate = 45.50m;

        // Act
        var technician = new Technician(employeeId, firstName, lastName, email, tenantId, hourlyRate);

        // Assert
        technician.EmployeeId.Should().Be(employeeId);
        technician.FirstName.Should().Be(firstName);
        technician.LastName.Should().Be(lastName);
        technician.Email.Should().Be(email);
        technician.TenantId.Should().Be(tenantId);
        technician.HourlyRate.Should().Be(hourlyRate);
        technician.FullName.Should().Be($"{firstName} {lastName}");
        technician.Status.Should().Be(TechnicianStatus.Active);
        technician.Phone.Should().BeNull();
        technician.HomeAddress.Should().BeNull();
        technician.CurrentLocation.Should().BeNull();
        technician.LastLocationUpdate.Should().BeNull();
        technician.Skills.Should().BeEmpty();
        technician.WorkingHours.Should().BeEmpty();
    }

    [Fact]
    public void Technician_Constructor_WithDefaultHourlyRate_ShouldInitializeCorrectly()
    {
        // Arrange
        var employeeId = "EMP001";
        var firstName = "John";
        var lastName = "Smith";
        var email = "john.smith@company.com";
        var tenantId = "test-tenant";

        // Act
        var technician = new Technician(employeeId, firstName, lastName, email, tenantId);

        // Assert
        technician.HourlyRate.Should().Be(0);
    }

    [Fact]
    public void UpdateContactInfo_ShouldUpdateAllContactFields()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var newFirstName = "Jane";
        var newLastName = "Doe";
        var newEmail = "jane.doe@company.com";
        var newPhone = "555-0123";
        var originalUpdatedAt = technician.UpdatedAt;

        // Act
        technician.UpdateContactInfo(newFirstName, newLastName, newEmail, newPhone);

        // Assert
        technician.FirstName.Should().Be(newFirstName);
        technician.LastName.Should().Be(newLastName);
        technician.Email.Should().Be(newEmail);
        technician.Phone.Should().Be(newPhone);
        technician.FullName.Should().Be($"{newFirstName} {newLastName}");
        technician.UpdatedAt.Should().BeAfter(originalUpdatedAt.Value);
    }

    [Fact]
    public void UpdateContactInfo_WithoutPhone_ShouldUpdateOtherFields()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var newFirstName = "Jane";
        var newLastName = "Doe";
        var newEmail = "jane.doe@company.com";

        // Act
        technician.UpdateContactInfo(newFirstName, newLastName, newEmail);

        // Assert
        technician.FirstName.Should().Be(newFirstName);
        technician.LastName.Should().Be(newLastName);
        technician.Email.Should().Be(newEmail);
        technician.Phone.Should().BeNull();
    }

    [Fact]
    public void UpdateLocation_ShouldSetLocationAndTimestamp()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var newLocation = new Coordinate(34.0522, -118.2437);
        var timestamp = DateTime.UtcNow.AddMinutes(-5);
        var originalUpdatedAt = technician.UpdatedAt;

        // Act
        technician.UpdateLocation(newLocation, timestamp);

        // Assert
        technician.CurrentLocation.Should().Be(newLocation);
        technician.LastLocationUpdate.Should().Be(timestamp);
        technician.UpdatedAt.Should().BeAfter(originalUpdatedAt.Value);
    }

    [Fact]
    public void UpdateLocation_WithoutTimestamp_ShouldUseCurrentTime()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var newLocation = new Coordinate(34.0522, -118.2437);
        var beforeUpdate = DateTime.UtcNow;

        // Act
        technician.UpdateLocation(newLocation);

        // Assert
        technician.CurrentLocation.Should().Be(newLocation);
        technician.LastLocationUpdate.Should().BeOnOrAfter(beforeUpdate);
        technician.LastLocationUpdate.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void SetHomeAddress_ShouldUpdateAddress()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var address = new Address("123 Main St", "Anytown", "CA", "12345", "USA", "123 Main St, Anytown, CA 12345", new Coordinate(34.0522, -118.2437));
        var originalUpdatedAt = technician.UpdatedAt;

        // Act
        technician.SetHomeAddress(address);

        // Assert
        technician.HomeAddress.Should().Be(address);
        technician.UpdatedAt.Should().BeAfter(originalUpdatedAt.Value);
    }

    [Fact]
    public void AddSkill_ShouldAddSkillToList()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var skill = "HVAC";
        var originalUpdatedAt = technician.UpdatedAt;

        // Act
        technician.AddSkill(skill);

        // Assert
        technician.Skills.Should().Contain(skill);
        technician.UpdatedAt.Should().BeAfter(originalUpdatedAt.Value);
    }

    [Fact]
    public void AddSkill_WhenSkillAlreadyExists_ShouldNotDuplicate()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var skill = "Electrical";
        technician.AddSkill(skill);
        var skillCountBefore = technician.Skills.Count;

        // Act
        technician.AddSkill(skill); // Try to add same skill again

        // Assert
        technician.Skills.Count.Should().Be(skillCountBefore);
        technician.Skills.Should().ContainSingle(s => s == skill);
    }

    [Fact]
    public void AddSkill_ShouldBeCaseInsensitive()
    {
        // Arrange
        var technician = CreateValidTechnician();
        technician.AddSkill("electrical");
        var skillCountBefore = technician.Skills.Count;

        // Act
        technician.AddSkill("ELECTRICAL");

        // Assert
        technician.Skills.Count.Should().Be(skillCountBefore);
    }

    [Fact]
    public void RemoveSkill_ShouldRemoveSkillFromList()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var skill = "Plumbing";
        technician.AddSkill(skill);

        // Act
        technician.RemoveSkill(skill);

        // Assert
        technician.Skills.Should().NotContain(skill);
    }

    [Fact]
    public void RemoveSkill_ShouldBeCaseInsensitive()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var skill = "Plumbing";
        technician.AddSkill(skill);

        // Act
        technician.RemoveSkill("PLUMBING");

        // Assert
        technician.Skills.Should().NotContain(skill);
    }

    [Fact]
    public void RemoveSkill_WhenSkillNotExists_ShouldNotThrow()
    {
        // Arrange
        var technician = CreateValidTechnician();

        // Act & Assert
        var act = () => technician.RemoveSkill("NonExistentSkill");
        act.Should().NotThrow();
    }

    [Fact]
    public void SetWorkingHours_ShouldReplaceExistingWorkingHours()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var newWorkingHours = new List<WorkingHours>
        {
            new(DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0)),
            new(DayOfWeek.Tuesday, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0)),
            new(DayOfWeek.Wednesday, new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0))
        };
        var originalUpdatedAt = technician.UpdatedAt;

        // Act
        technician.SetWorkingHours(newWorkingHours);

        // Assert
        technician.WorkingHours.Should().HaveCount(3);
        technician.WorkingHours.Should().BeEquivalentTo(newWorkingHours);
        technician.UpdatedAt.Should().BeAfter(originalUpdatedAt.Value);
    }

    [Theory]
    [InlineData(TechnicianStatus.Active)]
    [InlineData(TechnicianStatus.Inactive)]
    [InlineData(TechnicianStatus.OnLeave)]
    public void UpdateStatus_ShouldChangeStatus(TechnicianStatus newStatus)
    {
        // Arrange
        var technician = CreateValidTechnician();
        var originalUpdatedAt = technician.UpdatedAt;

        // Act
        technician.UpdateStatus(newStatus);

        // Assert
        technician.Status.Should().Be(newStatus);
        technician.UpdatedAt.Should().BeAfter(originalUpdatedAt.Value);
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 9, 0, true)]
    [InlineData(DayOfWeek.Monday, 7, 30, false)] // Before work hours
    [InlineData(DayOfWeek.Monday, 18, 0, false)] // After work hours
    [InlineData(DayOfWeek.Saturday, 9, 0, false)] // Weekend
    public void IsAvailableAt_ShouldReturnCorrectAvailability(DayOfWeek dayOfWeek, int hour, int minute, bool expectedAvailability)
    {
        // Arrange
        var technician = CreateValidTechnician();
        var testDate = GetNextWeekday(dayOfWeek).Date.Add(new TimeSpan(hour, minute, 0));

        // Act
        var isAvailable = technician.IsAvailableAt(testDate);

        // Assert
        isAvailable.Should().Be(expectedAvailability);
    }

    [Fact]
    public void IsAvailableAt_WhenInactive_ShouldReturnFalse()
    {
        // Arrange
        var technician = CreateValidTechnician();
        technician.UpdateStatus(TechnicianStatus.Inactive);
        var testDate = GetNextWeekday(DayOfWeek.Monday).Date.Add(new TimeSpan(9, 0, 0));

        // Act
        var isAvailable = technician.IsAvailableAt(testDate);

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsAvailableAt_WhenOnLeave_ShouldReturnFalse()
    {
        // Arrange
        var technician = CreateValidTechnician();
        technician.UpdateStatus(TechnicianStatus.OnLeave);
        var testDate = GetNextWeekday(DayOfWeek.Monday).Date.Add(new TimeSpan(9, 0, 0));

        // Act
        var isAvailable = technician.IsAvailableAt(testDate);

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsAvailableAt_WithNoWorkingHours_ShouldReturnFalse()
    {
        // Arrange
        var technician = new Technician("EMP001", "John", "Smith", "john@company.com", "test-tenant");
        // Don't set working hours
        var testDate = DateTime.Today.Add(new TimeSpan(9, 0, 0));

        // Act
        var isAvailable = technician.IsAvailableAt(testDate);

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 8, 0, 17, 0, 8, 30, true)]
    [InlineData(DayOfWeek.Monday, 8, 0, 17, 0, 17, 0, true)] // Exactly at end time
    [InlineData(DayOfWeek.Monday, 8, 0, 17, 0, 17, 1, false)] // One minute after end time
    [InlineData(DayOfWeek.Monday, 8, 0, 17, 0, 7, 59, false)] // One minute before start time
    public void IsAvailableAt_ShouldHandleBoundaryTimes(
        DayOfWeek dayOfWeek, 
        int startHour, int startMinute, 
        int endHour, int endMinute,
        int testHour, int testMinute,
        bool expectedAvailability)
    {
        // Arrange
        var technician = new Technician("EMP001", "John", "Smith", "john@company.com", "test-tenant");
        var workingHours = new List<WorkingHours>
        {
            new(dayOfWeek, new TimeSpan(startHour, startMinute, 0), new TimeSpan(endHour, endMinute, 0))
        };
        technician.SetWorkingHours(workingHours);
        
        var testDate = GetNextWeekday(dayOfWeek).Date.Add(new TimeSpan(testHour, testMinute, 0));

        // Act
        var isAvailable = technician.IsAvailableAt(testDate);

        // Assert
        isAvailable.Should().Be(expectedAvailability);
    }

    [Theory]
    [DomainAutoData]
    public void Technician_PropertyChanges_ShouldUpdateTimestamp(
        string employeeId,
        string firstName,
        string lastName,
        string email,
        string tenantId)
    {
        // Arrange
        var technician = new Technician(employeeId, firstName, lastName, email, tenantId);
        technician.UpdatedAt.Should().BeNull(); // Initially null

        // Act & Assert - Test multiple operations update the timestamp
        technician.UpdateContactInfo("New First", "New Last", "new@email.com");
        technician.UpdatedAt.Should().NotBeNull();
        var timestamp1 = technician.UpdatedAt!.Value;

        technician.AddSkill("New Skill");
        technician.UpdatedAt.Should().NotBeNull();
        technician.UpdatedAt.Should().BeAfter(timestamp1);
        var timestamp2 = technician.UpdatedAt!.Value;

        technician.UpdateStatus(TechnicianStatus.Inactive);
        technician.UpdatedAt.Should().NotBeNull();
        technician.UpdatedAt.Should().BeAfter(timestamp2);
    }

    [Fact]
    public void Technician_MultipleSkillOperations_ShouldMaintainConsistency()
    {
        // Arrange
        var technician = CreateValidTechnician();
        var skills = new[] { "HVAC", "Carpentry" }; // CreateValidTechnician already adds "Electrical" and "Plumbing"

        // Act
        foreach (var skill in skills)
        {
            technician.AddSkill(skill);
        }

        // Assert
        technician.Skills.Should().HaveCount(4); // 2 from CreateValidTechnician + 2 new ones
        technician.Skills.Should().Contain(skills);
        technician.Skills.Should().Contain("Electrical"); // From CreateValidTechnician
        technician.Skills.Should().Contain("Plumbing"); // From CreateValidTechnician

        // Remove some skills
        technician.RemoveSkill("Electrical");
        technician.RemoveSkill("HVAC");

        // Assert
        technician.Skills.Should().HaveCount(2);
        technician.Skills.Should().NotContain("Electrical");
        technician.Skills.Should().NotContain("HVAC");
        technician.Skills.Should().Contain("Plumbing");
        technician.Skills.Should().Contain("Carpentry");
    }

    private static DateTime GetNextWeekday(DayOfWeek dayOfWeek)
    {
        var today = DateTime.Today;
        var daysUntilTarget = ((int)dayOfWeek - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilTarget == 0 && today.DayOfWeek == dayOfWeek)
            return today;
        return today.AddDays(daysUntilTarget == 0 ? 7 : daysUntilTarget);
    }
}
