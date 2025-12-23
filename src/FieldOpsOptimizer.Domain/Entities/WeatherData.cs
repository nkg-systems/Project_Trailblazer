using FieldOpsOptimizer.Domain.Common;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Domain.Entities;

public class WeatherData : BaseEntity
{
    public Coordinate Location { get; private set; } = null!;
    public DateTime Timestamp { get; private set; }
    public DateTime ValidUntil { get; private set; }
    public WeatherCondition Condition { get; private set; }
    public double Temperature { get; private set; } // Celsius
    public double FeelsLike { get; private set; } // Celsius
    public double Humidity { get; private set; } // Percentage
    public double WindSpeed { get; private set; } // km/h
    public double WindDirection { get; private set; } // Degrees
    public double Pressure { get; private set; } // hPa
    public double Visibility { get; private set; } // km
    public double UvIndex { get; private set; }
    public double PrecipitationChance { get; private set; } // Percentage
    public double PrecipitationAmount { get; private set; } // mm
    public string Description { get; private set; } = string.Empty;
    public string IconCode { get; private set; } = string.Empty;
    public WeatherSeverity Severity { get; private set; }
    public bool IsSuitableForFieldWork { get; private set; }
    public string? WorkSafetyNotes { get; private set; }
    public string Source { get; private set; } = string.Empty; // API provider
    public string TenantId { get; private set; } = string.Empty;

    private WeatherData() { } // For EF Core

    public WeatherData(
        Coordinate location,
        DateTime timestamp,
        DateTime validUntil,
        WeatherCondition condition,
        double temperature,
        double humidity,
        double windSpeed,
        string description,
        string source,
        string tenantId)
    {
        Location = location;
        Timestamp = timestamp;
        ValidUntil = validUntil;
        Condition = condition;
        Temperature = temperature;
        Humidity = humidity;
        WindSpeed = windSpeed;
        Description = description;
        Source = source;
        TenantId = tenantId;
        
        // Calculate derived properties
        CalculateWorkSuitability();
    }

    public void UpdateWeatherData(
        WeatherCondition condition,
        double temperature,
        double humidity,
        double windSpeed,
        string description)
    {
        Condition = condition;
        Temperature = temperature;
        Humidity = humidity;
        WindSpeed = windSpeed;
        Description = description;
        
        CalculateWorkSuitability();
        UpdateTimestamp();
    }

    public void SetDetailedMetrics(
        double feelsLike,
        double windDirection,
        double pressure,
        double visibility,
        double uvIndex,
        double precipitationChance,
        double precipitationAmount,
        string iconCode)
    {
        FeelsLike = feelsLike;
        WindDirection = windDirection;
        Pressure = pressure;
        Visibility = visibility;
        UvIndex = uvIndex;
        PrecipitationChance = precipitationChance;
        PrecipitationAmount = precipitationAmount;
        IconCode = iconCode;
        
        CalculateWorkSuitability();
        UpdateTimestamp();
    }

    private void CalculateWorkSuitability()
    {
        // Business logic to determine if conditions are suitable for field work
        var isTemperatureOk = Temperature >= -10 && Temperature <= 45; // Celsius
        var isWindOk = WindSpeed <= 50; // km/h
        var isPrecipitationOk = PrecipitationChance <= 80;
        var isVisibilityOk = Visibility >= 1; // km
        var isConditionOk = Condition != WeatherCondition.Storm && Condition != WeatherCondition.Extreme;

        IsSuitableForFieldWork = isTemperatureOk && isWindOk && isPrecipitationOk && isVisibilityOk && isConditionOk;

        // Set severity
        Severity = CalculateSeverity();

        // Generate safety notes
        WorkSafetyNotes = GenerateSafetyNotes();
    }

    private WeatherSeverity CalculateSeverity()
    {
        if (Condition == WeatherCondition.Extreme || WindSpeed > 70 || Temperature < -15 || Temperature > 50)
            return WeatherSeverity.Extreme;

        if (Condition == WeatherCondition.Storm || WindSpeed > 50 || PrecipitationChance > 80 || 
            Temperature < -5 || Temperature > 40)
            return WeatherSeverity.Severe;

        if (WindSpeed > 30 || PrecipitationChance > 60 || Temperature < 0 || Temperature > 35)
            return WeatherSeverity.Moderate;

        return WeatherSeverity.Mild;
    }

    private string? GenerateSafetyNotes()
    {
        var notes = new List<string>();

        if (Temperature < 0)
            notes.Add("Cold weather - ensure appropriate protective clothing");
        if (Temperature > 35)
            notes.Add("Hot weather - ensure adequate hydration and sun protection");
        if (WindSpeed > 30)
            notes.Add("High winds - secure equipment and materials");
        if (PrecipitationChance > 60)
            notes.Add("High chance of rain - waterproof equipment recommended");
        if (UvIndex > 7)
            notes.Add("High UV index - sun protection required");
        if (Visibility < 2)
            notes.Add("Poor visibility - exercise extra caution");

        return notes.Count > 0 ? string.Join("; ", notes) : null;
    }

    public bool IsExpired => DateTime.UtcNow > ValidUntil;

    public bool IsCurrentlyValid => DateTime.UtcNow >= Timestamp && DateTime.UtcNow <= ValidUntil;
}
