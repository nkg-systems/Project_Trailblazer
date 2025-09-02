using AutoFixture;
using AutoFixture.Xunit2;
using Bogus;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;
using FluentAssertions;

namespace FieldOpsOptimizer.Domain.Tests.TestBase;

/// <summary>
/// Base class for domain unit tests with common utilities and data builders
/// </summary>
public abstract class DomainTestBase : IDisposable
{
    protected readonly IFixture Fixture;
    protected readonly Faker Faker;

    protected DomainTestBase()
    {
        Fixture = new Fixture();
        Faker = new Faker();
        
        // Configure AutoFixture to ignore navigation properties and circular references
        Fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));
        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        
        ConfigureFixture();
    }

    protected virtual void ConfigureFixture()
    {
        // Configure value object creation
        Fixture.Register(() => new Coordinate(
            Faker.Address.Latitude(),
            Faker.Address.Longitude()));

        Fixture.Register(() => new Address(
            Faker.Address.StreetAddress(),
            Faker.Address.City(),
            Faker.Address.StateAbbr(),
            Faker.Address.ZipCode(),
            "USA",
            Faker.Address.FullAddress(),
            Fixture.Create<Coordinate>()));

        // Configure entity creation
        Fixture.Register(() => new Technician(
            Faker.Random.AlphaNumeric(8),
            Faker.Name.FirstName(),
            Faker.Name.LastName(),
            Faker.Internet.Email(),
            "test-tenant",
            Faker.Random.Decimal(25, 100)));

        Fixture.Register(() => new ServiceJob(
            GenerateJobNumber(),
            Faker.Company.CompanyName(),
            Fixture.Create<Address>(),
            Faker.Lorem.Paragraph(),
            Faker.Date.Future(),
            TimeSpan.FromHours(Faker.Random.Double(1, 8)),
            "test-tenant",
            Faker.PickRandom<JobPriority>()));

        Fixture.Register(() => new Route(
            Faker.Vehicle.Model(),
            Faker.Date.Future(),
            Guid.NewGuid(),
            "test-tenant",
            Faker.PickRandom<OptimizationObjective>()));
    }

    protected string GenerateJobNumber()
    {
        var today = DateTime.Today;
        var random = Faker.Random.Int(1, 999);
        return $"JOB{today:yyyyMMdd}-{random:D3}";
    }

    protected Technician CreateValidTechnician()
    {
        var technician = new Technician(
            Faker.Random.AlphaNumeric(8),
            Faker.Name.FirstName(),
            Faker.Name.LastName(),
            Faker.Internet.Email(),
            "test-tenant",
            Faker.Random.Decimal(25, 100));

        // Add some skills
        technician.AddSkill("Electrical");
        technician.AddSkill("Plumbing");

        // Set working hours
        var workingHours = new List<WorkingHours>
        {
            new(DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
            new(DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
            new(DayOfWeek.Wednesday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
            new(DayOfWeek.Thursday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
            new(DayOfWeek.Friday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0))
        };
        technician.SetWorkingHours(workingHours);

        return technician;
    }

    protected ServiceJob CreateValidServiceJob()
    {
        var address = new Address(
            Faker.Address.StreetAddress(),
            Faker.Address.City(),
            Faker.Address.StateAbbr(),
            Faker.Address.ZipCode(),
            "USA",
            Faker.Address.FullAddress(),
            new Coordinate(Faker.Address.Latitude(), Faker.Address.Longitude()));

        var job = new ServiceJob(
            GenerateJobNumber(),
            Faker.Company.CompanyName(),
            address,
            Faker.Lorem.Paragraph(),
            Faker.Date.Future(),
            TimeSpan.FromHours(Faker.Random.Double(1, 8)),
            "test-tenant",
            Faker.PickRandom<JobPriority>());

        job.AddRequiredSkill("Electrical");
        job.AddTag("Maintenance");

        return job;
    }

    protected Route CreateValidRoute()
    {
        var route = new Route(
            Faker.Vehicle.Model() + " Route",
            Faker.Date.Future(),
            Guid.NewGuid(),
            "test-tenant",
            OptimizationObjective.MinimizeDistance);

        return route;
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Custom AutoData attribute for domain tests
/// </summary>
public class DomainAutoDataAttribute : AutoDataAttribute
{
    public DomainAutoDataAttribute() : base(() => 
    {
        var fixture = new Fixture();
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        
        return fixture;
    })
    {
    }
}
