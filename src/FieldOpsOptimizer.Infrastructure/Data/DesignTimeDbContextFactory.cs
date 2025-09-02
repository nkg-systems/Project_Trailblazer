using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FieldOpsOptimizer.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating ApplicationDbContext instances during migrations
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings.json and environment variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../FieldOpsOptimizer.Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        // Try to get connection string, fallback to default PostgreSQL if not found
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Database=FieldOpsOptimizer;Username=fieldops;Password=fieldops123;";

        builder.UseNpgsql(connectionString, options =>
        {
            options.MigrationsAssembly("FieldOpsOptimizer.Infrastructure");
            options.CommandTimeout(30);
        });

        // Enable sensitive data logging in development
        if (args.Contains("--verbose") || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            builder.EnableSensitiveDataLogging();
            builder.EnableDetailedErrors();
        }

        return new ApplicationDbContext(builder.Options);
    }
}
