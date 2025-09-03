namespace FieldOpsOptimizer.Application.Common.Models;

/// <summary>
/// Configuration settings for weather service integration
/// </summary>
public class WeatherSettings
{
    public const string ConfigurationSection = "Weather";

    /// <summary>
    /// OpenWeatherMap API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for weather API (default: OpenWeatherMap)
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5";

    /// <summary>
    /// API timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for failed requests
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Cache duration for weather data in minutes
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Cache duration for forecast data in minutes
    /// </summary>
    public int ForecastCacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Default units for weather data (metric/imperial)
    /// </summary>
    public string Units { get; set; } = "metric";

    /// <summary>
    /// Language for weather descriptions
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Enable weather alerts monitoring
    /// </summary>
    public bool EnableAlerts { get; set; } = true;

    /// <summary>
    /// Enable weather-based job recommendations
    /// </summary>
    public bool EnableJobRecommendations { get; set; } = true;

    /// <summary>
    /// Default radius for weather alerts (km)
    /// </summary>
    public double DefaultAlertRadiusKm { get; set; } = 50;

    /// <summary>
    /// Minimum temperature threshold for field work (Celsius)
    /// </summary>
    public double MinWorkTemperature { get; set; } = -10;

    /// <summary>
    /// Maximum temperature threshold for field work (Celsius)
    /// </summary>
    public double MaxWorkTemperature { get; set; } = 45;

    /// <summary>
    /// Maximum wind speed for safe field work (km/h)
    /// </summary>
    public double MaxWorkWindSpeed { get; set; } = 50;

    /// <summary>
    /// Maximum precipitation chance for comfortable field work (%)
    /// </summary>
    public double MaxWorkPrecipitationChance { get; set; } = 80;

    /// <summary>
    /// Minimum visibility for safe field work (km)
    /// </summary>
    public double MinWorkVisibility { get; set; } = 1.0;

    /// <summary>
    /// Enable automatic weather data refresh
    /// </summary>
    public bool EnableAutoRefresh { get; set; } = true;

    /// <summary>
    /// Auto refresh interval in minutes
    /// </summary>
    public int AutoRefreshIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Rate limiting: maximum requests per minute
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Enable detailed logging for weather operations
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
