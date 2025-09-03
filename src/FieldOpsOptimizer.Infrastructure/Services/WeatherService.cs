using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FieldOpsOptimizer.Infrastructure.Services;

/// <summary>
/// Comprehensive weather service implementation
/// </summary>
public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<WeatherForecast> GetCurrentWeatherAsync(
        Coordinate location, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting current weather for location {Latitude}, {Longitude}", 
            location.Latitude, location.Longitude);

        // For now, return a mock weather forecast
        // In production, this would call Open-Meteo API
        return new WeatherForecast(
            location,
            DateTime.UtcNow,
            TemperatureCelsius: 22.0,
            TemperatureFahrenheit: 71.6,
            Humidity: 65,
            WindSpeedKmh: 15.0,
            WindSpeedMph: 9.3,
            WindDirection: 180,
            PrecipitationMm: 0.0,
            PrecipitationIn: 0.0,
            Condition: WeatherCondition.Cloudy,
            Description: "Partly cloudy",
            Visibility: 10.0,
            CloudCover: 40.0,
            UvIndex: 5.0);
    }

    public async Task<WeatherForecast> GetForecastAsync(
        Coordinate location, 
        DateTime dateTime, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting weather forecast for location {Latitude}, {Longitude} at {DateTime}", 
            location.Latitude, location.Longitude, dateTime);

        // Mock implementation - in production would call weather API
        return new WeatherForecast(
            location,
            dateTime,
            TemperatureCelsius: 20.0,
            TemperatureFahrenheit: 68.0,
            Humidity: 70,
            WindSpeedKmh: 10.0,
            WindSpeedMph: 6.2,
            WindDirection: 160,
            PrecipitationMm: 2.0,
            PrecipitationIn: 0.08,
            Condition: WeatherCondition.Rain,
            Description: "Light rain",
            Visibility: 8.0,
            CloudCover: 80.0,
            UvIndex: 2.0);
    }

    public async Task<List<WeatherForecast>> GetHourlyForecastAsync(
        Coordinate location, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting hourly forecast for location {Latitude}, {Longitude} from {StartDate} to {EndDate}", 
            location.Latitude, location.Longitude, startDate, endDate);

        var forecasts = new List<WeatherForecast>();
        var currentHour = startDate;

        while (currentHour <= endDate)
        {
            forecasts.Add(await GetForecastAsync(location, currentHour, cancellationToken));
            currentHour = currentHour.AddHours(1);
        }

        return forecasts;
    }

    public async Task<WeatherData?> GetCurrentWeatherDataAsync(
        Coordinate location, 
        string tenantId, 
        CancellationToken cancellationToken = default)
    {
        var forecast = await GetCurrentWeatherAsync(location, cancellationToken);
        return MapForecastToWeatherData(forecast, tenantId);
    }

    public async Task<IEnumerable<WeatherData>> GetWeatherForecastDataAsync(
        Coordinate location, 
        DateTime startDate, 
        DateTime endDate, 
        string tenantId, 
        CancellationToken cancellationToken = default)
    {
        var forecasts = await GetHourlyForecastAsync(location, startDate, endDate, cancellationToken);
        return forecasts.Select(f => MapForecastToWeatherData(f, tenantId)).Where(wd => wd != null)!;
    }

    public async Task<IDictionary<Coordinate, WeatherForecast?>> GetWeatherForLocationsAsync(
        IEnumerable<Coordinate> locations, 
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<Coordinate, WeatherForecast?>();
        
        foreach (var location in locations)
        {
            try
            {
                var weather = await GetCurrentWeatherAsync(location, cancellationToken);
                results[location] = weather;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get weather for location {Latitude}, {Longitude}", 
                    location.Latitude, location.Longitude);
                results[location] = null;
            }
        }

        return results;
    }

    public async Task<WeatherWorkSuitability> IsWeatherSuitableForWorkAsync(
        Coordinate location, 
        DateTime dateTime, 
        CancellationToken cancellationToken = default)
    {
        var weather = await GetForecastAsync(location, dateTime, cancellationToken);
        
        var isSuitable = weather.IsSuitableForFieldWork;
        var severity = DetermineSeverity(weather);
        var safetyConsiderations = GenerateSafetyConsiderations(weather);
        var recommendedAction = GenerateRecommendedAction(weather, isSuitable);

        return new WeatherWorkSuitability
        {
            IsSuitable = isSuitable,
            Severity = severity,
            RecommendedAction = recommendedAction,
            SafetyConsiderations = safetyConsiderations,
            WeatherData = weather
        };
    }

    public async Task<IEnumerable<WeatherAlert>> GetWeatherAlertsAsync(
        Coordinate location, 
        double radiusKm, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting weather alerts for location {Latitude}, {Longitude} within {RadiusKm}km", 
            location.Latitude, location.Longitude, radiusKm);

        // Mock implementation - in production would fetch from weather service
        return new List<WeatherAlert>();
    }

    public async Task<JobWeatherRecommendation> GetJobSchedulingRecommendationAsync(
        Coordinate jobLocation, 
        DateTime proposedDateTime, 
        TimeSpan estimatedDuration, 
        JobPriority priority, 
        CancellationToken cancellationToken = default)
    {
        var currentWeather = await GetCurrentWeatherAsync(jobLocation, cancellationToken);
        var forecastWeather = await GetForecastAsync(jobLocation, proposedDateTime, cancellationToken);

        var isRecommended = forecastWeather.IsSuitableForFieldWork && 
                           (priority == JobPriority.Emergency || forecastWeather.WorkEfficiencyFactor > 0.7);

        var confidenceLevel = DetermineConfidenceLevel(forecastWeather, proposedDateTime);
        var considerations = GenerateJobConsiderations(forecastWeather, estimatedDuration, priority);
        var alternativeTime = isRecommended ? null : FindAlternativeTime(jobLocation, proposedDateTime, estimatedDuration);

        return new JobWeatherRecommendation
        {
            IsRecommended = isRecommended,
            Recommendation = isRecommended ? "Weather conditions are suitable for this job" : 
                            "Consider rescheduling due to unfavorable weather conditions",
            ConfidenceLevel = confidenceLevel,
            Considerations = considerations,
            AlternativeTime = await alternativeTime,
            CurrentWeather = currentWeather,
            ForecastAtTime = forecastWeather
        };
    }

    public async Task<IEnumerable<OptimalWeatherWindow>> GetOptimalWeatherWindowsAsync(
        IEnumerable<JobLocationRequest> jobRequests, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default)
    {
        var windows = new List<OptimalWeatherWindow>();

        foreach (var jobRequest in jobRequests)
        {
            var forecasts = await GetHourlyForecastAsync(jobRequest.Location, startDate, endDate, cancellationToken);
            
            // Find windows where weather is suitable
            var suitableForecasts = forecasts.Where(f => f.IsSuitableForFieldWork).ToList();
            
            // Group consecutive hours into windows
            var currentWindow = new List<WeatherForecast>();
            foreach (var forecast in suitableForecasts.OrderBy(f => f.DateTime))
            {
                if (!currentWindow.Any() || 
                    forecast.DateTime.Subtract(currentWindow.Last().DateTime).TotalHours <= 1)
                {
                    currentWindow.Add(forecast);
                }
                else
                {
                    // Create window from current group
                    if (currentWindow.Count >= Math.Ceiling(jobRequest.EstimatedDuration.TotalHours))
                    {
                        windows.Add(CreateOptimalWindow(currentWindow, jobRequest));
                    }
                    currentWindow = new List<WeatherForecast> { forecast };
                }
            }

            // Handle the last window
            if (currentWindow.Count >= Math.Ceiling(jobRequest.EstimatedDuration.TotalHours))
            {
                windows.Add(CreateOptimalWindow(currentWindow, jobRequest));
            }
        }

        return windows.OrderByDescending(w => w.OptimalityScore);
    }

    private WeatherData MapForecastToWeatherData(WeatherForecast forecast, string tenantId)
    {
        return new WeatherData(
            forecast.Location,
            forecast.DateTime,
            forecast.TemperatureCelsius,
            forecast.Humidity / 100.0,
            forecast.WindSpeedKmh,
            forecast.PrecipitationMm,
            forecast.Condition,
            tenantId);
    }

    private WeatherSeverity DetermineSeverity(WeatherForecast weather)
    {
        return weather.Condition switch
        {
            WeatherCondition.Extreme => WeatherSeverity.Extreme,
            WeatherCondition.Storm => WeatherSeverity.Severe,
            WeatherCondition.Rain when weather.PrecipitationMm > 10 => WeatherSeverity.Moderate,
            WeatherCondition.Snow when weather.PrecipitationMm > 5 => WeatherSeverity.Moderate,
            _ => WeatherSeverity.Mild
        };
    }

    private List<string> GenerateSafetyConsiderations(WeatherForecast weather)
    {
        var considerations = new List<string>();

        if (weather.WindSpeedKmh > 30)
            considerations.Add("High winds present - secure loose equipment");

        if (weather.PrecipitationMm > 5)
            considerations.Add("Wet conditions - use non-slip footwear and take extra caution");

        if (weather.Visibility < 5)
            considerations.Add("Poor visibility - use additional lighting and proceed with caution");

        if (weather.UvIndex > 7)
            considerations.Add("High UV levels - use sun protection");

        if (weather.TemperatureCelsius > 30 || weather.TemperatureCelsius < 5)
            considerations.Add("Extreme temperatures - dress appropriately and take regular breaks");

        return considerations;
    }

    private string GenerateRecommendedAction(WeatherForecast weather, bool isSuitable)
    {
        if (isSuitable)
        {
            return weather.WorkEfficiencyFactor > 0.9 ? "Proceed as planned" : "Proceed with caution";
        }

        return weather.Condition switch
        {
            WeatherCondition.Storm => "Postpone until storm passes",
            WeatherCondition.Extreme => "Do not proceed - wait for improved conditions",
            WeatherCondition.Rain when weather.PrecipitationMm > 15 => "Consider rescheduling to avoid heavy rain",
            _ => "Monitor conditions and consider rescheduling"
        };
    }

    private WeatherConfidenceLevel DetermineConfidenceLevel(WeatherForecast weather, DateTime proposedDateTime)
    {
        var hoursUntil = (proposedDateTime - DateTime.UtcNow).TotalHours;
        
        return hoursUntil switch
        {
            < 6 => WeatherConfidenceLevel.VeryHigh,
            < 24 => WeatherConfidenceLevel.High,
            < 72 => WeatherConfidenceLevel.Medium,
            _ => WeatherConfidenceLevel.Low
        };
    }

    private List<string> GenerateJobConsiderations(WeatherForecast weather, TimeSpan duration, JobPriority priority)
    {
        var considerations = new List<string>();

        if (weather.WorkEfficiencyFactor < 0.8)
            considerations.Add($"Reduced work efficiency expected ({weather.WorkEfficiencyFactor:P0})");

        if (duration.TotalHours > 4 && weather.Condition == WeatherCondition.Rain)
            considerations.Add("Long duration job in rain - consider breaking into shorter sessions");

        if (priority == JobPriority.Emergency)
            considerations.Add("Emergency priority - proceed with appropriate safety measures");

        return considerations;
    }

    private async Task<DateTime?> FindAlternativeTime(Coordinate location, DateTime originalTime, TimeSpan duration)
    {
        // Look for better weather in the next 48 hours
        var searchEnd = originalTime.AddHours(48);
        var forecasts = await GetHourlyForecastAsync(location, originalTime, searchEnd);

        var suitableForecasts = forecasts
            .Where(f => f.IsSuitableForFieldWork && f.WorkEfficiencyFactor > 0.8)
            .OrderByDescending(f => f.WorkEfficiencyFactor)
            .ToList();

        return suitableForecasts.FirstOrDefault()?.DateTime;
    }

    private OptimalWeatherWindow CreateOptimalWindow(List<WeatherForecast> forecasts, JobLocationRequest jobRequest)
    {
        var avgEfficiency = forecasts.Average(f => f.WorkEfficiencyFactor);
        var optimalityScore = avgEfficiency * 100;

        // Boost score for preferred times
        var preferredHour = jobRequest.PreferredStartTime.Hour;
        var windowHour = forecasts.First().DateTime.Hour;
        if (Math.Abs(windowHour - preferredHour) <= 2)
            optimalityScore += 10;

        return new OptimalWeatherWindow
        {
            Location = jobRequest.Location,
            StartTime = forecasts.First().DateTime,
            EndTime = forecasts.Last().DateTime,
            OptimalityScore = optimalityScore,
            ExpectedCondition = forecasts.GroupBy(f => f.Condition).OrderByDescending(g => g.Count()).First().Key,
            Reasoning = $"Weather efficiency: {avgEfficiency:P0}, suitable for {forecasts.Count} hours",
            SuitableJobIds = new List<Guid> { jobRequest.JobId }
        };
    }
}
