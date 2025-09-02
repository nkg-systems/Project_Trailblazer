using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FieldOpsOptimizer.Infrastructure.Data;
using Bogus;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace FieldOpsOptimizer.Api.Tests.TestBase;

/// <summary>
/// Base class for API integration tests using WebApplicationFactory
/// </summary>
public class ApiIntegrationTestBase : IClassFixture<CustomWebApplicationFactory<Program>>, IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    protected readonly ApplicationDbContext Context;
    protected readonly Faker Faker;

    public ApiIntegrationTestBase(CustomWebApplicationFactory<Program> factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Context = factory.Services.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Faker = new Faker();
    }

    public async Task InitializeAsync()
    {
        // Ensure database is created and clean for each test
        await Context.Database.EnsureCreatedAsync();
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up after each test
        await CleanupTestDataAsync();
    }

    protected virtual async Task SeedTestDataAsync()
    {
        // Override in derived classes to add specific test data
        await Task.CompletedTask;
    }

    protected virtual async Task CleanupTestDataAsync()
    {
        // Remove test data
        Context.ServiceJobs.RemoveRange(Context.ServiceJobs);
        Context.Technicians.RemoveRange(Context.Technicians);
        Context.Routes.RemoveRange(Context.Routes);
        await Context.SaveChangesAsync();
    }

    protected Technician CreateTestTechnician()
    {
        var technician = new Technician(
            Faker.Random.AlphaNumeric(8),
            Faker.Name.FirstName(),
            Faker.Name.LastName(),
            Faker.Internet.Email(),
            "test-tenant",
            Faker.Random.Decimal(25, 100));

        technician.AddSkill("Electrical");
        technician.AddSkill("Plumbing");

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

    protected ServiceJob CreateTestServiceJob()
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
            DateTime.Today.AddDays(Faker.Random.Int(1, 30)),
            TimeSpan.FromHours(Faker.Random.Double(1, 8)),
            "test-tenant",
            Faker.PickRandom<JobPriority>());

        job.AddRequiredSkill("Electrical");
        job.AddTag("Maintenance");

        return job;
    }

    protected string GenerateJobNumber()
    {
        var today = DateTime.Today;
        var random = Faker.Random.Int(1, 999);
        return $"JOB{today:yyyyMMdd}-{random:D3}";
    }

    protected async Task<T> PostAsync<T>(string endpoint, object payload) where T : class
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await Client.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseContent, JsonOptions)!;
    }

    protected async Task<T> GetAsync<T>(string endpoint) where T : class
    {
        var response = await Client.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
    }

    protected async Task<T> PutAsync<T>(string endpoint, object payload) where T : class
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await Client.PutAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseContent, JsonOptions)!;
    }

    protected async Task DeleteAsync(string endpoint)
    {
        var response = await Client.DeleteAsync(endpoint);
        response.EnsureSuccessStatusCode();
    }

    protected static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// Custom WebApplicationFactory for integration tests
/// </summary>
public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real database context
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForTesting");
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            // Add test authentication
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            // Override any external services with test doubles if needed
            // services.Replace(ServiceDescriptor.Scoped<IExternalService, MockExternalService>());

            // Ensure the database is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }
}

/// <summary>
/// Test authentication handler for integration tests
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim("tenant_id", "test-tenant")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
