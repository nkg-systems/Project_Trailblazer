using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Application.Common.Models;
using FieldOpsOptimizer.Domain.Entities;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Infrastructure.Services;

/// <summary>
/// Weather service implementation using OpenWeatherMap API
/// </summary>
public class OpenWeatherMapService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OpenWeatherMapService> _logger;
    private readonly WeatherSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenWeatherMapService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<OpenWeatherMapService> logger,
        IOptions<WeatherSettings> settings)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _settings = settings.Value;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    public async Task<WeatherForecast> GetCurrentWeatherAsync(
        Coordinate location, 
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"current_weather_{location.Latitude}_{location.Longitude}";
        
        if (_cache.TryGetValue(cacheKey, out WeatherForecast? cachedWeather) && cachedWeather != null)
        {
            _logger.LogDebug("Returning cached weather data for {Location}", location);
            return cachedWeather;
        }

        try
        {
            var url = $"/weather?lat={location.Latitude}&lon={location.Longitude}&appid={_settings.ApiKey}&units={_settings.Units}&lang={_settings.Language}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var weatherResponse = JsonSerializer.Deserialize<OpenWeatherMapResponse>(json, _jsonOptions);

            if (weatherResponse == null)
                throw new InvalidOperationException("Failed to deserialize weather response");

            var weather = MapToWeatherForecast(weatherResponse, location);
            
            // Cache the result
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.CacheDurationMinutes)
            };
            _cache.Set(cacheKey, weather, cacheOptions);

            _logger.LogInformation("Retrieved current weather for {Location}: {Condition} {Temperature}°C", 
                location, weather.Condition, weather.TemperatureCelsius);

            return weather;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current weather for {Location}", location);
            throw;
        }
    }

    public async Task<WeatherForecast> GetForecastAsync(
        Coordinate location, 
        DateTime dateTime, 
        CancellationToken cancellationToken = default)
    {
        // For specific date/time, get the closest forecast from hourly data
        var startDate = dateTime.AddHours(-1);
        var endDate = dateTime.AddHours(1);
        
        var forecasts = await GetHourlyForecastAsync(location, startDate, endDate, cancellationToken);
        
        // Find the closest forecast to the requested time
        return forecasts
            .OrderBy(f => Math.Abs((f.DateTime - dateTime).TotalMinutes))
            .FirstOrDefault() ?? throw new InvalidOperationException($"No forecast available for {dateTime}");
    }

    public async Task<List<WeatherForecast>> GetHourlyForecastAsync(
        Coordinate location, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"forecast_{location.Latitude}_{location.Longitude}_{startDate:yyyyMMddHH}_{endDate:yyyyMMddHH}";
        
        if (_cache.TryGetValue(cacheKey, out List<WeatherForecast>? cachedForecast) && cachedForecast != null)
        {
            _logger.LogDebug("Returning cached forecast data for {Location}", location);
            return cachedForecast;
        }

        try
        {
            var url = $"/forecast?lat={location.Latitude}&lon={location.Longitude}&appid={_settings.ApiKey}&units={_settings.Units}&lang={_settings.Language}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var forecastResponse = JsonSerializer.Deserialize<OpenWeatherMapForecastResponse>(json, _jsonOptions);

            if (forecastResponse == null || forecastResponse.List == null)
                throw new InvalidOperationException("Failed to deserialize forecast response");

            var forecasts = forecastResponse.List
                .Where(f => f.Dt >= ((DateTimeOffset)startDate).ToUnixTimeSeconds() && 
                           f.Dt <= ((DateTimeOffset)endDate).ToUnixTimeSeconds())
                .Select(f => MapToWeatherForecast(f, location))
                .ToList();

            // Cache the result
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.ForecastCacheDurationMinutes)
            };
            _cache.Set(cacheKey, forecasts, cacheOptions);

            _logger.LogInformation("Retrieved {Count} forecast entries for {Location}", forecasts.Count, location);

            return forecasts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving forecast for {Location}", location);
            throw;
        }
    }

    public async Task<WeatherData?> GetCurrentWeatherDataAsync(
        Coordinate location, 
        string tenantId, 
        CancellationToken cancellationToken = default)
    {
        var forecast = await GetCurrentWeatherAsync(location, cancellationToken);
        
        var weatherData = new WeatherData(
            location: location,
            timestamp: DateTime.UtcNow,
            validUntil: DateTime.UtcNow.AddMinutes(_settings.CacheDurationMinutes),
            condition: forecast.Condition,
            temperature: forecast.TemperatureCelsius,
            humidity: forecast.Humidity,
            windSpeed: forecast.WindSpeedKmh,
            description: forecast.Description,
            source: "OpenWeatherMap",
            tenantId: tenantId
        );

        weatherData.SetDetailedMetrics(
            feelsLike: forecast.TemperatureCelsius, // We could enhance this with actual feels-like data
            windDirection: forecast.WindDirection,
            pressure: 1013.25, // Default pressure - could be enhanced with actual data
            visibility: forecast.Visibility,
            uvIndex: forecast.UvIndex,
            precipitationChance: forecast.PrecipitationMm > 0 ? 80 : 20, // Simplified logic
            precipitationAmount: forecast.PrecipitationMm,
            iconCode: "01d" // Could be enhanced with actual icon codes
        );

        return weatherData;
    }

    public async Task<IEnumerable<WeatherData>> GetWeatherForecastDataAsync(
        Coordinate location, 
        DateTime startDate, 
        DateTime endDate, 
        string tenantId, 
        CancellationToken cancellationToken = default)
    {
        var forecasts = await GetHourlyForecastAsync(location, startDate, endDate, cancellationToken);
        
        return forecasts.Select(f => new WeatherData(
            location: location,
            timestamp: f.DateTime,
            validUntil: f.DateTime.AddHours(1),
            condition: f.Condition,
            temperature: f.TemperatureCelsius,
            humidity: f.Humidity,
            windSpeed: f.WindSpeedKmh,
            description: f.Description,
            source: "OpenWeatherMap",
            tenantId: tenantId
        ));
    }

    public async Task<IDictionary<Coordinate, WeatherForecast?>> GetWeatherForLocationsAsync(
        IEnumerable<Coordinate> locations, 
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Coordinate, WeatherForecast?>();
        
        // Process locations in batches to avoid overwhelming the API
        var locationBatches = locations.Chunk(5); // Process 5 locations at a time
        
        foreach (var batch in locationBatches)
        {
            var tasks = batch.Select(async location =>
            {
                try
                {
                    var weather = await GetCurrentWeatherAsync(location, cancellationToken);
                    return new KeyValuePair<Coordinate, WeatherForecast?>(location, weather);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get weather for location {Location}", location);
                    return new KeyValuePair<Coordinate, WeatherForecast?>(location, null);
                }
            });

            var batchResults = await Task.WhenAll(tasks);
            
            foreach (var kvp in batchResults)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Add small delay between batches to respect rate limits
            if (locationBatches.Count() > 1)
                await Task.Delay(100, cancellationToken);
        }

        return result;
    }

    public async Task<WeatherWorkSuitability> IsWeatherSuitableForWorkAsync(
        Coordinate location, 
        DateTime dateTime, 
        CancellationToken cancellationToken = default)
    {
        var weather = dateTime <= DateTime.UtcNow.AddHours(1) 
            ? await GetCurrentWeatherAsync(location, cancellationToken)
            : await GetForecastAsync(location, dateTime, cancellationToken);

        var suitability = new WeatherWorkSuitability
        {
            WeatherData = weather,
            IsSuitable = weather.IsSuitableForFieldWork,
            Severity = MapToWeatherSeverity(weather),
            SafetyConsiderations = GenerateSafetyConsiderations(weather),
            RecommendedAction = GenerateRecommendedAction(weather)
        };

        return suitability;
    }

    public Task<IEnumerable<WeatherAlert>> GetWeatherAlertsAsync(
        Coordinate location, 
        double radiusKm, 
        CancellationToken cancellationToken = default)
    {
        // OpenWeatherMap's OneCall API includes alerts, but for simplicity,
        // we'll return empty alerts for now. This could be enhanced with actual alert data.
        _logger.LogInformation("Weather alerts requested for {Location} within {RadiusKm}km", location, radiusKm);
        return Task.FromResult(Enumerable.Empty<WeatherAlert>());
    }

    public async Task<JobWeatherRecommendation> GetJobSchedulingRecommendationAsync(
        Coordinate jobLocation, 
        DateTime proposedDateTime, 
        TimeSpan estimatedDuration, 
        JobPriority priority, 
        CancellationToken cancellationToken = default)
    {
        var currentWeather = await GetCurrentWeatherAsync(jobLocation, cancellationToken);
        var forecastWeather = proposedDateTime > DateTime.UtcNow.AddHours(1) 
            ? await GetForecastAsync(jobLocation, proposedDateTime, cancellationToken)
            : null;

        var weatherToEvaluate = forecastWeather ?? currentWeather;
        var isRecommended = EvaluateJobRecommendation(weatherToEvaluate, priority, estimatedDuration);

        return new JobWeatherRecommendation
        {
            IsRecommended = isRecommended,
            Recommendation = GenerateJobRecommendation(weatherToEvaluate, isRecommended, priority),
            ConfidenceLevel = forecastWeather != null ? WeatherConfidenceLevel.Medium : WeatherConfidenceLevel.High,
            Considerations = GenerateJobConsiderations(weatherToEvaluate, estimatedDuration),
            AlternativeTime = isRecommended ? null : await FindBetterJobTime(jobLocation, proposedDateTime, estimatedDuration, cancellationToken),
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

        foreach (var request in jobRequests)
        {
            try
            {
                var forecasts = await GetHourlyForecastAsync(request.Location, startDate, endDate, cancellationToken);
                var optimalWindows = FindOptimalWindows(request, forecasts);
                windows.AddRange(optimalWindows);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to find optimal weather windows for job {JobId}", request.JobId);
            }
        }

        return windows.OrderByDescending(w => w.OptimalityScore);
    }

    #region Private Helper Methods

    private static WeatherForecast MapToWeatherForecast(OpenWeatherMapResponse response, Coordinate location)
    {
        return new WeatherForecast(
            Location: location,
            DateTime: DateTimeOffset.FromUnixTimeSeconds(response.Dt).DateTime,
            TemperatureCelsius: response.Main.Temp,
            TemperatureFahrenheit: response.Main.Temp * 9 / 5 + 32,
            Humidity: (int)response.Main.Humidity,
            WindSpeedKmh: response.Wind?.Speed ?? 0 * 3.6, // Convert m/s to km/h
            WindSpeedMph: (response.Wind?.Speed ?? 0) * 2.237, // Convert m/s to mph
            WindDirection: (int)(response.Wind?.Deg ?? 0),
            PrecipitationMm: response.Rain?.OneHour ?? response.Snow?.OneHour ?? 0,
            PrecipitationIn: (response.Rain?.OneHour ?? response.Snow?.OneHour ?? 0) * 0.0393701,
            Condition: MapWeatherCondition(response.Weather.FirstOrDefault()?.Main ?? "Clear"),
            Description: response.Weather.FirstOrDefault()?.Description ?? "Clear sky",
            Visibility: response.Visibility / 1000.0, // Convert meters to km
            CloudCover: response.Clouds?.All ?? 0,
            UvIndex: 0 // Not available in basic weather endpoint
        );
    }

    private static WeatherForecast MapToWeatherForecast(ForecastItem item, Coordinate location)
    {
        return new WeatherForecast(
            Location: location,
            DateTime: DateTimeOffset.FromUnixTimeSeconds(item.Dt).DateTime,
            TemperatureCelsius: item.Main.Temp,
            TemperatureFahrenheit: item.Main.Temp * 9 / 5 + 32,
            Humidity: (int)item.Main.Humidity,
            WindSpeedKmh: item.Wind?.Speed ?? 0 * 3.6,
            WindSpeedMph: (item.Wind?.Speed ?? 0) * 2.237,
            WindDirection: (int)(item.Wind?.Deg ?? 0),
            PrecipitationMm: item.Rain?.ThreeHour ?? item.Snow?.ThreeHour ?? 0,
            PrecipitationIn: (item.Rain?.ThreeHour ?? item.Snow?.ThreeHour ?? 0) * 0.0393701,
            Condition: MapWeatherCondition(item.Weather.FirstOrDefault()?.Main ?? "Clear"),
            Description: item.Weather.FirstOrDefault()?.Description ?? "Clear sky",
            Visibility: item.Visibility / 1000.0,
            CloudCover: item.Clouds?.All ?? 0,
            UvIndex: 0
        );
    }

    private static WeatherCondition MapWeatherCondition(string openWeatherCondition)
    {
        return openWeatherCondition.ToLowerInvariant() switch
        {
            "clear" => WeatherCondition.Clear,
            "clouds" => WeatherCondition.Cloudy,
            "rain" => WeatherCondition.Rain,
            "drizzle" => WeatherCondition.Rain,
            "thunderstorm" => WeatherCondition.Storm,
            "snow" => WeatherCondition.Snow,
            "mist" or "fog" or "haze" => WeatherCondition.Fog,
            "tornado" or "hurricane" => WeatherCondition.Extreme,
            _ => WeatherCondition.Clear
        };
    }

    private static WeatherSeverity MapToWeatherSeverity(WeatherForecast weather)
    {
        if (weather.Condition == WeatherCondition.Extreme || weather.WindSpeedKmh > 70 || 
            weather.TemperatureCelsius < -15 || weather.TemperatureCelsius > 50)
            return WeatherSeverity.Extreme;

        if (weather.Condition == WeatherCondition.Storm || weather.WindSpeedKmh > 50 || 
            weather.PrecipitationMm > 10 || weather.TemperatureCelsius < -5 || weather.TemperatureCelsius > 40)
            return WeatherSeverity.Severe;

        if (weather.WindSpeedKmh > 30 || weather.PrecipitationMm > 5 || 
            weather.TemperatureCelsius < 0 || weather.TemperatureCelsius > 35)
            return WeatherSeverity.Moderate;

        return WeatherSeverity.Mild;
    }

    private static List<string> GenerateSafetyConsiderations(WeatherForecast weather)
    {
        var considerations = new List<string>();

        if (weather.TemperatureCelsius < 0)
            considerations.Add("Cold weather - ensure appropriate protective clothing");
        if (weather.TemperatureCelsius > 35)
            considerations.Add("Hot weather - ensure adequate hydration and sun protection");
        if (weather.WindSpeedKmh > 30)
            considerations.Add("High winds - secure equipment and materials");
        if (weather.PrecipitationMm > 5)
            considerations.Add("Precipitation expected - waterproof equipment recommended");
        if (weather.Visibility < 2)
            considerations.Add("Poor visibility - exercise extra caution");

        return considerations;
    }

    private static string GenerateRecommendedAction(WeatherForecast weather)
    {
        if (!weather.IsSuitableForFieldWork)
            return "Consider postponing non-urgent field work";
        
        if (weather.WorkEfficiencyFactor < 0.8)
            return "Proceed with increased safety precautions";
        
        return "Conditions are suitable for field work";
    }

    private static bool EvaluateJobRecommendation(WeatherForecast weather, JobPriority priority, TimeSpan duration)
    {
        if (priority == JobPriority.Emergency)
            return true; // Emergency jobs proceed regardless of weather

        if (!weather.IsSuitableForFieldWork)
            return false;

        // For longer jobs, be more conservative
        if (duration.TotalHours > 4 && weather.WorkEfficiencyFactor < 0.85)
            return false;

        return weather.WorkEfficiencyFactor >= 0.7;
    }

    private static string GenerateJobRecommendation(WeatherForecast weather, bool isRecommended, JobPriority priority)
    {
        if (priority == JobPriority.Emergency)
            return "Emergency priority - proceed with appropriate safety measures";

        if (isRecommended)
            return $"Weather conditions are suitable. Efficiency factor: {weather.WorkEfficiencyFactor:P0}";

        return $"Weather conditions may impact work quality. Consider rescheduling. Current conditions: {weather.Description}";
    }

    private static List<string> GenerateJobConsiderations(WeatherForecast weather, TimeSpan duration)
    {
        var considerations = new List<string>();

        if (weather.WorkEfficiencyFactor < 1.0)
            considerations.Add($"Expected efficiency reduction: {(1 - weather.WorkEfficiencyFactor):P0}");

        if (duration.TotalHours > 4)
            considerations.Add("Extended duration job - monitor weather changes");

        if (weather.PrecipitationMm > 0)
            considerations.Add("Precipitation may cause delays or safety concerns");

        return considerations;
    }

    private async Task<DateTime?> FindBetterJobTime(
        Coordinate location, 
        DateTime originalTime, 
        TimeSpan duration, 
        CancellationToken cancellationToken)
    {
        try
        {
            var searchStart = originalTime.AddHours(-12);
            var searchEnd = originalTime.AddHours(36);
            var forecasts = await GetHourlyForecastAsync(location, searchStart, searchEnd, cancellationToken);

            var bestTime = forecasts
                .Where(f => f.IsSuitableForFieldWork)
                .OrderByDescending(f => f.WorkEfficiencyFactor)
                .FirstOrDefault();

            return bestTime?.DateTime;
        }
        catch
        {
            return null;
        }
    }

    private static List<OptimalWeatherWindow> FindOptimalWindows(JobLocationRequest request, List<WeatherForecast> forecasts)
    {
        var windows = new List<OptimalWeatherWindow>();
        
        for (int i = 0; i < forecasts.Count - 1; i++)
        {
            var startForecast = forecasts[i];
            if (!startForecast.IsSuitableForFieldWork) continue;

            var windowEnd = startForecast.DateTime.Add(request.EstimatedDuration);
            var endIndex = forecasts.FindIndex(i, f => f.DateTime >= windowEnd);
            
            if (endIndex == -1) endIndex = forecasts.Count - 1;

            var windowForecasts = forecasts.Skip(i).Take(endIndex - i + 1);
            var avgEfficiency = windowForecasts.Average(f => f.WorkEfficiencyFactor);
            var allSuitable = windowForecasts.All(f => f.IsSuitableForFieldWork);

            if (allSuitable)
            {
                windows.Add(new OptimalWeatherWindow
                {
                    Location = request.Location,
                    StartTime = startForecast.DateTime,
                    EndTime = windowEnd,
                    OptimalityScore = avgEfficiency * 100,
                    ExpectedCondition = startForecast.Condition,
                    Reasoning = $"Suitable weather window with {avgEfficiency:P0} efficiency",
                    SuitableJobIds = new List<Guid> { request.JobId }
                });
            }
        }

        return windows;
    }

    #endregion
}

#region OpenWeatherMap Response Models

public class OpenWeatherMapResponse
{
    public long Dt { get; set; }
    public MainWeatherData Main { get; set; } = null!;
    public List<WeatherInfo> Weather { get; set; } = new();
    public WindInfo? Wind { get; set; }
    public CloudsInfo? Clouds { get; set; }
    public RainInfo? Rain { get; set; }
    public SnowInfo? Snow { get; set; }
    public double Visibility { get; set; }
}

public class OpenWeatherMapForecastResponse
{
    public List<ForecastItem> List { get; set; } = new();
}

public class ForecastItem
{
    public long Dt { get; set; }
    public MainWeatherData Main { get; set; } = null!;
    public List<WeatherInfo> Weather { get; set; } = new();
    public WindInfo? Wind { get; set; }
    public CloudsInfo? Clouds { get; set; }
    public RainInfo? Rain { get; set; }
    public SnowInfo? Snow { get; set; }
    public double Visibility { get; set; }
}

public class MainWeatherData
{
    public double Temp { get; set; }
    public double Humidity { get; set; }
    public double Pressure { get; set; }
}

public class WeatherInfo
{
    public string Main { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class WindInfo
{
    public double Speed { get; set; }
    public double Deg { get; set; }
}

public class CloudsInfo
{
    public double All { get; set; }
}

public class RainInfo
{
    [JsonPropertyName("1h")]
    public double OneHour { get; set; }
    
    [JsonPropertyName("3h")]
    public double ThreeHour { get; set; }
}

public class SnowInfo
{
    [JsonPropertyName("1h")]
    public double OneHour { get; set; }
    
    [JsonPropertyName("3h")]
    public double ThreeHour { get; set; }
}

#endregion
