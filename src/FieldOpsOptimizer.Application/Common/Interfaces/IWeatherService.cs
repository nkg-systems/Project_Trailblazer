using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Application.Common.Interfaces;

/// <summary>
/// Service for retrieving and managing weather data for field operations
/// </summary>
public interface IWeatherService
{
    // Enhanced core weather methods
    Task<WeatherForecast> GetCurrentWeatherAsync(
        Coordinate location,
        CancellationToken cancellationToken = default);

    Task<WeatherForecast> GetForecastAsync(
        Coordinate location,
        DateTime dateTime,
        CancellationToken cancellationToken = default);

    Task<List<WeatherForecast>> GetHourlyForecastAsync(
        Coordinate location,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    // New comprehensive weather methods
    /// <summary>
    /// Get current weather data as domain entity for a specific location
    /// </summary>
    Task<WeatherData?> GetCurrentWeatherDataAsync(Coordinate location, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get weather forecast for a specific location and date range
    /// </summary>
    Task<IEnumerable<WeatherData>> GetWeatherForecastDataAsync(
        Coordinate location, 
        DateTime startDate, 
        DateTime endDate, 
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get weather data for multiple locations (batch request)
    /// </summary>
    Task<IDictionary<Coordinate, WeatherForecast?>> GetWeatherForLocationsAsync(
        IEnumerable<Coordinate> locations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if weather conditions are suitable for field work at a specific location and time
    /// </summary>
    Task<WeatherWorkSuitability> IsWeatherSuitableForWorkAsync(
        Coordinate location, 
        DateTime dateTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get weather alerts for a specific area
    /// </summary>
    Task<IEnumerable<WeatherAlert>> GetWeatherAlertsAsync(
        Coordinate location, 
        double radiusKm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get weather-based recommendations for job scheduling
    /// </summary>
    Task<JobWeatherRecommendation> GetJobSchedulingRecommendationAsync(
        Coordinate jobLocation, 
        DateTime proposedDateTime, 
        TimeSpan estimatedDuration,
        JobPriority priority,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the best weather windows for a list of jobs
    /// </summary>
    Task<IEnumerable<OptimalWeatherWindow>> GetOptimalWeatherWindowsAsync(
        IEnumerable<JobLocationRequest> jobRequests,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

public record WeatherForecast(
    Coordinate Location,
    DateTime DateTime,
    double TemperatureCelsius,
    double TemperatureFahrenheit,
    int Humidity,
    double WindSpeedKmh,
    double WindSpeedMph,
    int WindDirection,
    double PrecipitationMm,
    double PrecipitationIn,
    WeatherCondition Condition,
    string Description,
    double Visibility,
    double CloudCover,
    double UvIndex)
{
    public bool IsSuitableForFieldWork => 
        Condition != WeatherCondition.Storm &&
        Condition != WeatherCondition.Extreme &&
        WindSpeedKmh < 50 && // Less than 50 km/h wind
        Visibility > 1.0; // Greater than 1km visibility

    public double WorkEfficiencyFactor => Condition switch
    {
        WeatherCondition.Clear => 1.0,
        WeatherCondition.Cloudy => 0.95,
        WeatherCondition.Rain when PrecipitationMm < 5 => 0.85,
        WeatherCondition.Rain => 0.70,
        WeatherCondition.Snow when PrecipitationMm < 10 => 0.75,
        WeatherCondition.Snow => 0.60,
        WeatherCondition.Fog => 0.80,
        WeatherCondition.Storm => 0.30,
        WeatherCondition.Extreme => 0.20,
        _ => 0.90
    };
}

/// <summary>
/// Represents the suitability of weather conditions for field work
/// </summary>
public class WeatherWorkSuitability
{
    public bool IsSuitable { get; set; }
    public WeatherSeverity Severity { get; set; }
    public string? RecommendedAction { get; set; }
    public List<string> SafetyConsiderations { get; set; } = new();
    public WeatherForecast WeatherData { get; set; } = null!;
}

/// <summary>
/// Represents a weather alert
/// </summary>
public class WeatherAlert
{
    public string AlertId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WeatherAlertSeverity Severity { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Coordinate Location { get; set; } = null!;
    public double AffectedRadiusKm { get; set; }
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Weather-based recommendation for job scheduling
/// </summary>
public class JobWeatherRecommendation
{
    public bool IsRecommended { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public WeatherConfidenceLevel ConfidenceLevel { get; set; }
    public List<string> Considerations { get; set; } = new();
    public DateTime? AlternativeTime { get; set; }
    public WeatherForecast CurrentWeather { get; set; } = null!;
    public WeatherForecast? ForecastAtTime { get; set; }
}

/// <summary>
/// Represents an optimal weather window for scheduling jobs
/// </summary>
public class OptimalWeatherWindow
{
    public Coordinate Location { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double OptimalityScore { get; set; } // 0-100
    public WeatherCondition ExpectedCondition { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<Guid> SuitableJobIds { get; set; } = new();
}

/// <summary>
/// Request for job location weather analysis
/// </summary>
public class JobLocationRequest
{
    public Guid JobId { get; set; }
    public Coordinate Location { get; set; } = null!;
    public DateTime PreferredStartTime { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public JobPriority Priority { get; set; }
    public List<string> WeatherConstraints { get; set; } = new(); // e.g., "no_rain", "low_wind"
}

/// <summary>
/// Weather alert severity levels
/// </summary>
public enum WeatherAlertSeverity
{
    Advisory = 1,
    Watch = 2,
    Warning = 3,
    Emergency = 4
}

/// <summary>
/// Confidence levels for weather predictions
/// </summary>
public enum WeatherConfidenceLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    VeryHigh = 4
}
