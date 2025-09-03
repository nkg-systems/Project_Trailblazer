using FieldOpsOptimizer.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FieldOpsOptimizer.Api.DTOs;

/// <summary>
/// DTO for weather forecast data
/// </summary>
public class WeatherForecastDto
{
    public CoordinateDto Location { get; set; } = null!;
    public DateTime DateTime { get; set; }
    public double TemperatureCelsius { get; set; }
    public double TemperatureFahrenheit { get; set; }
    public int Humidity { get; set; }
    public double WindSpeedKmh { get; set; }
    public double WindSpeedMph { get; set; }
    public int WindDirection { get; set; }
    public double PrecipitationMm { get; set; }
    public double PrecipitationIn { get; set; }
    public WeatherCondition Condition { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Visibility { get; set; }
    public double CloudCover { get; set; }
    public double UvIndex { get; set; }
    public bool IsSuitableForFieldWork { get; set; }
    public double WorkEfficiencyFactor { get; set; }
}

/// <summary>
/// DTO for weather work suitability assessment
/// </summary>
public class WeatherWorkSuitabilityDto
{
    public bool IsSuitable { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string? RecommendedAction { get; set; }
    public List<string> SafetyConsiderations { get; set; } = new();
    public WeatherForecastDto Weather { get; set; } = null!;
}

/// <summary>
/// DTO for job scheduling request with weather considerations
/// </summary>
public class JobSchedulingRequestDto
{
    [Required]
    public double Latitude { get; set; }
    
    [Required]
    public double Longitude { get; set; }
    
    [Required]
    public DateTime ProposedDateTime { get; set; }
    
    [Required]
    [Range(0.1, 24.0)]
    public double EstimatedDurationHours { get; set; }
    
    [Required]
    public JobPriority Priority { get; set; }
    
    public List<string> WeatherConstraints { get; set; } = new();
}

/// <summary>
/// DTO for job weather recommendation response
/// </summary>
public class JobWeatherRecommendationDto
{
    public bool IsRecommended { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string ConfidenceLevel { get; set; } = string.Empty;
    public List<string> Considerations { get; set; } = new();
    public DateTime? AlternativeTime { get; set; }
    public WeatherForecastDto CurrentWeather { get; set; } = null!;
    public WeatherForecastDto? ForecastAtTime { get; set; }
}

/// <summary>
/// DTO for batch weather request
/// </summary>
public class BatchWeatherRequestDto
{
    [Required]
    [MinLength(1)]
    public List<CoordinateDto> Locations { get; set; } = new();
}

/// <summary>
/// DTO for location weather response
/// </summary>
public class LocationWeatherDto
{
    public CoordinateDto Location { get; set; } = null!;
    public WeatherForecastDto? Weather { get; set; }
    public bool IsAvailable { get; set; }
}

/// <summary>
/// DTO for optimal weather windows request
/// </summary>
public class OptimalWeatherWindowsRequestDto
{
    [Required]
    [MinLength(1)]
    public List<JobLocationRequestDto> JobRequests { get; set; } = new();
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
}

/// <summary>
/// DTO for job location request within optimal weather windows
/// </summary>
public class JobLocationRequestDto
{
    [Required]
    public Guid JobId { get; set; }
    
    [Required]
    public double Latitude { get; set; }
    
    [Required]
    public double Longitude { get; set; }
    
    [Required]
    public DateTime PreferredStartTime { get; set; }
    
    [Required]
    [Range(0.1, 24.0)]
    public double EstimatedDurationHours { get; set; }
    
    [Required]
    public JobPriority Priority { get; set; }
    
    public List<string> WeatherConstraints { get; set; } = new();
}

/// <summary>
/// DTO for optimal weather window response
/// </summary>
public class OptimalWeatherWindowDto
{
    public CoordinateDto Location { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double OptimalityScore { get; set; }
    public WeatherCondition ExpectedCondition { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<Guid> SuitableJobIds { get; set; } = new();
}

/// <summary>
/// DTO for weather alert
/// </summary>
public class WeatherAlertDto
{
    public string AlertId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public CoordinateDto Location { get; set; } = null!;
    public double AffectedRadiusKm { get; set; }
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// DTO for weather-based job insights
/// </summary>
public class WeatherJobInsightsDto
{
    public int TotalJobsAnalyzed { get; set; }
    public int JobsSuitableForCurrentWeather { get; set; }
    public int JobsRequiringRescheduling { get; set; }
    public double AverageWeatherEfficiencyFactor { get; set; }
    public List<string> TopWeatherConcerns { get; set; } = new();
    public List<OptimalWeatherWindowDto> RecommendedWindows { get; set; } = new();
    public DateTime AnalysisTimestamp { get; set; }
}
