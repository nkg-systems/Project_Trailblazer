using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Infrastructure.Services;

namespace FieldOpsOptimizer.Infrastructure.Data;

/// <summary>
/// Database seeding service for initial data population
/// </summary>
public class DatabaseSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(ApplicationDbContext context, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Seeds the database with initial data
    /// </summary>
    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting database seeding...");

            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Seed users first (required for technicians)
            await SeedUsersAsync();
            
            // Seed technicians
            await SeedTechniciansAsync();
            
            // Seed sample jobs
            await SeedServiceJobsAsync();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during database seeding");
            throw;
        }
    }

    /// <summary>
    /// Seeds initial users
    /// </summary>
    private async Task SeedUsersAsync()
    {
        if (await _context.Users.AnyAsync())
        {
            _logger.LogInformation("Users already exist, skipping user seeding");
            return;
        }

        _logger.LogInformation("Seeding initial users...");

        var authService = new AuthService(
            null!, 
            null!, 
            Microsoft.Extensions.Options.Options.Create(new FieldOpsOptimizer.Application.Common.Models.JwtSettings()),
            _logger as ILogger<AuthService> ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);

        var users = new[]
        {
            new User(
                "admin",
                "admin@fieldops.com",
                authService.HashPassword("Admin123!"),
                "System",
                "Administrator",
                "default-tenant",
                UserRole.Admin),
                
            new User(
                "dispatcher",
                "dispatcher@fieldops.com", 
                authService.HashPassword("Dispatch123!"),
                "John",
                "Dispatcher",
                "default-tenant",
                UserRole.Dispatcher),
                
            new User(
                "manager",
                "manager@fieldops.com",
                authService.HashPassword("Manager123!"),
                "Sarah",
                "Manager",
                "default-tenant",
                UserRole.Manager),
                
            new User(
                "tech1",
                "tech1@fieldops.com",
                authService.HashPassword("Tech123!"),
                "Mike",
                "Johnson",
                "default-tenant",
                UserRole.Technician),
                
            new User(
                "tech2",
                "tech2@fieldops.com",
                authService.HashPassword("Tech123!"),
                "Lisa",
                "Smith",
                "default-tenant",
                UserRole.Technician)
        };

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} users", users.Length);
    }

    /// <summary>
    /// Seeds initial technicians
    /// </summary>
    private async Task SeedTechniciansAsync()
    {
        if (await _context.Technicians.AnyAsync())
        {
            _logger.LogInformation("Technicians already exist, skipping technician seeding");
            return;
        }

        _logger.LogInformation("Seeding initial technicians...");

        // Get the technician users we just created
        var tech1User = await _context.Users.FirstAsync(u => u.Username == "tech1");
        var tech2User = await _context.Users.FirstAsync(u => u.Username == "tech2");

        var homeAddress1 = new Address(
            "123 Main St",
            "Seattle",
            "WA",
            "98101",
            "USA",
            "123 Main St, Seattle, WA 98101",
            new Coordinate(47.6062, -122.3321));

        var homeAddress2 = new Address(
            "456 Oak Ave",
            "Portland",
            "OR",
            "97201",
            "USA",
            "456 Oak Ave, Portland, OR 97201",
            new Coordinate(45.5152, -122.6784));

        var technician1 = new Technician(
            "EMP001",
            "Mike",
            "Johnson",
            "mike.johnson@fieldops.com",
            "default-tenant",
            25.50m);

        var technician2 = new Technician(
            "EMP002", 
            "Lisa",
            "Smith",
            "lisa.smith@fieldops.com",
            "default-tenant",
            28.75m);

        // Set home addresses
        technician1.SetHomeAddress(homeAddress1);
        technician2.SetHomeAddress(homeAddress2);
        
        // Update contact info with phone numbers
        technician1.UpdateContactInfo("Mike", "Johnson", "mike.johnson@fieldops.com", "+1-555-0101");
        technician2.UpdateContactInfo("Lisa", "Smith", "lisa.smith@fieldops.com", "+1-555-0102");
        
        // Add skills
        technician1.AddSkill("HVAC");
        technician1.AddSkill("Electrical");
        technician1.AddSkill("Plumbing");

        technician2.AddSkill("HVAC");
        technician2.AddSkill("Refrigeration");
        technician2.AddSkill("Electrical");

        // Set working hours (Monday to Friday, 8 AM to 5 PM)
        var workingHoursList = new List<WorkingHours>();
        for (int day = 1; day <= 5; day++) // Monday = 1, Friday = 5
        {
            workingHoursList.Add(new WorkingHours(
                (DayOfWeek)day,
                new TimeSpan(8, 0, 0),  // 8:00 AM
                new TimeSpan(17, 0, 0))); // 5:00 PM
        }
        
        technician1.SetWorkingHours(workingHoursList);
        technician2.SetWorkingHours(workingHoursList);

        _context.Technicians.AddRange(technician1, technician2);
        await _context.SaveChangesAsync();

        // Link users to technicians
        tech1User.LinkToTechnician(technician1.Id);
        tech2User.LinkToTechnician(technician2.Id);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded 2 technicians with linked user accounts");
    }

    /// <summary>
    /// Seeds sample service jobs
    /// </summary>
    private async Task SeedServiceJobsAsync()
    {
        if (await _context.ServiceJobs.AnyAsync())
        {
            _logger.LogInformation("Service jobs already exist, skipping job seeding");
            return;
        }

        _logger.LogInformation("Seeding sample service jobs...");

        var technicians = await _context.Technicians.ToListAsync();
        var tech1 = technicians.FirstOrDefault();

        var addresses = new[]
        {
            new Address("789 Pine St", "Seattle", "WA", "98102", "USA", 
                "789 Pine St, Seattle, WA 98102", new Coordinate(47.6205, -122.3493)),
            new Address("321 Elm St", "Seattle", "WA", "98103", "USA",
                "321 Elm St, Seattle, WA 98103", new Coordinate(47.6587, -122.3140)),
            new Address("654 Cedar Ave", "Bellevue", "WA", "98004", "USA",
                "654 Cedar Ave, Bellevue, WA 98004", new Coordinate(47.6101, -122.2015))
        };

        var jobs = new[]
        {
            new ServiceJob(
                "JOB20240101-001",
                "Acme Corporation",
                addresses[0],
                "HVAC system maintenance and filter replacement",
                DateTime.Today.AddDays(1).AddHours(9),
                TimeSpan.FromHours(2),
                "default-tenant",
                JobPriority.Medium),
                
            new ServiceJob(
                "JOB20240101-002", 
                "Tech Solutions Inc",
                addresses[1],
                "Electrical panel inspection and repair",
                DateTime.Today.AddDays(2).AddHours(10),
                TimeSpan.FromHours(3),
                "default-tenant",
                JobPriority.High),
                
            new ServiceJob(
                "JOB20240101-003",
                "Green Energy Corp",
                addresses[2],
                "Plumbing system emergency repair",
                DateTime.Today.AddDays(1).AddHours(14),
                TimeSpan.FromHours(4),
                "default-tenant", 
                JobPriority.Emergency)
        };

        // Configure job details
        jobs[0].AddRequiredSkill("HVAC");
        jobs[0].AddTag("Maintenance");
        jobs[0].AddTag("Routine");

        jobs[1].AddRequiredSkill("Electrical");
        jobs[1].AddTag("Inspection");
        jobs[1].AddTag("Safety");

        jobs[2].AddRequiredSkill("Plumbing");
        jobs[2].AddTag("Emergency");
        jobs[2].AddTag("Repair");

        // Assign first technician to first job
        if (tech1 != null)
        {
            jobs[0].AssignTechnician(tech1.Id);
        }

        _context.ServiceJobs.AddRange(jobs);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} sample service jobs", jobs.Length);
    }

    /// <summary>
    /// Applies pending migrations to the database
    /// </summary>
    public async Task MigrateDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("Applying database migrations...");
            
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Found {Count} pending migrations: {Migrations}", 
                    pendingMigrations.Count(), string.Join(", ", pendingMigrations));
                
                await _context.Database.MigrateAsync();
                _logger.LogInformation("Database migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("No pending migrations found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during database migration");
            throw;
        }
    }

    /// <summary>
    /// Checks if the database can be connected to
    /// </summary>
    public async Task<bool> CanConnectAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot connect to database");
            return false;
        }
    }
}
