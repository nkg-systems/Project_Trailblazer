using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FieldOpsOptimizer.Domain.Entities;

namespace FieldOpsOptimizer.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core configuration for WeatherData entity
/// </summary>
public class WeatherDataConfiguration : IEntityTypeConfiguration<WeatherData>
{
    public void Configure(EntityTypeBuilder<WeatherData> builder)
    {
        // Primary key
        builder.HasKey(wd => wd.Id);

        // Required properties
        builder.Property(wd => wd.TenantId)
            .IsRequired()
            .HasMaxLength(100)
            .HasComment("Tenant ID for multi-tenant isolation");

        builder.Property(wd => wd.Source)
            .IsRequired()
            .HasMaxLength(100)
            .HasComment("Weather data provider (e.g., OpenWeatherMap, AccuWeather)");

        builder.Property(wd => wd.Description)
            .IsRequired()
            .HasMaxLength(500)
            .HasComment("Human-readable weather description");

        builder.Property(wd => wd.IconCode)
            .HasMaxLength(20)
            .HasComment("Weather icon identifier from the provider");

        // Enum conversions
        builder.Property(wd => wd.Condition)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired()
            .HasComment("Primary weather condition");

        builder.Property(wd => wd.Severity)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasComment("Weather severity level for field work assessment");

        // Decimal precision for weather metrics
        builder.Property(wd => wd.Temperature)
            .HasPrecision(5, 2)
            .HasComment("Temperature in Celsius");

        builder.Property(wd => wd.FeelsLike)
            .HasPrecision(5, 2)
            .HasComment("Perceived temperature in Celsius");

        builder.Property(wd => wd.Humidity)
            .HasPrecision(5, 2)
            .HasComment("Relative humidity percentage (0-100)");

        builder.Property(wd => wd.WindSpeed)
            .HasPrecision(6, 2)
            .HasComment("Wind speed in km/h");

        builder.Property(wd => wd.WindDirection)
            .HasPrecision(5, 2)
            .HasComment("Wind direction in degrees (0-360)");

        builder.Property(wd => wd.Pressure)
            .HasPrecision(7, 2)
            .HasComment("Atmospheric pressure in hPa");

        builder.Property(wd => wd.Visibility)
            .HasPrecision(6, 2)
            .HasComment("Visibility distance in kilometers");

        builder.Property(wd => wd.UvIndex)
            .HasPrecision(4, 2)
            .HasComment("UV index (0-11+)");

        builder.Property(wd => wd.PrecipitationChance)
            .HasPrecision(5, 2)
            .HasComment("Chance of precipitation percentage (0-100)");

        builder.Property(wd => wd.PrecipitationAmount)
            .HasPrecision(6, 2)
            .HasComment("Expected precipitation amount in mm");

        // Boolean fields
        builder.Property(wd => wd.IsSuitableForFieldWork)
            .IsRequired()
            .HasDefaultValue(true)
            .HasComment("Whether weather conditions are suitable for field work");

        // Optional work safety notes
        builder.Property(wd => wd.WorkSafetyNotes)
            .HasMaxLength(1000)
            .HasComment("Generated safety notes for field workers");

        // DateTime fields
        builder.Property(wd => wd.Timestamp)
            .IsRequired()
            .HasComment("When this weather data was recorded");

        builder.Property(wd => wd.ValidUntil)
            .IsRequired()
            .HasComment("When this weather data expires");

        // Configure Location as owned entity (Coordinate value object)
        builder.OwnsOne(wd => wd.Location, coord =>
        {
            coord.Property(c => c.Latitude)
                .HasPrecision(10, 7)
                .IsRequired()
                .HasComment("Geographic latitude");

            coord.Property(c => c.Longitude)
                .HasPrecision(10, 7)
                .IsRequired()
                .HasComment("Geographic longitude");
        });

        // Indexes for performance and querying
        
        // Primary index for tenant isolation
        builder.HasIndex(wd => wd.TenantId)
            .HasDatabaseName("IX_WeatherData_TenantId");

        // Index for time-based queries and cleanup
        builder.HasIndex(wd => new { wd.TenantId, wd.ValidUntil, wd.Timestamp })
            .HasDatabaseName("IX_WeatherData_Tenant_Validity");

        // Index for current weather queries
        builder.HasIndex(wd => new { wd.TenantId, wd.IsSuitableForFieldWork, wd.ValidUntil })
            .HasDatabaseName("IX_WeatherData_Tenant_Suitability_Valid")
            .HasFilter("ValidUntil > GETUTCDATE()");

        // Index for weather condition analysis
        builder.HasIndex(wd => new { wd.TenantId, wd.Condition, wd.Severity })
            .HasDatabaseName("IX_WeatherData_Tenant_Condition_Severity");

        // Index for weather source and timestamp for deduplication
        builder.HasIndex(wd => new { wd.Source, wd.Timestamp })
            .HasDatabaseName("IX_WeatherData_Source_Time");

        // Note: Location-based indexes with owned entities can be added manually in SQL if needed
        // or configured differently using spatial data types in future versions

        // Table configuration
        builder.ToTable("WeatherData", wb => wb.HasComment("Weather data for field operation planning and safety assessment"));
    }
}