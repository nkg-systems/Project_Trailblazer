using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Infrastructure.ExternalServices;

public class OpenMeteoWeatherService_Legacy
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoWeatherService_Legacy> _logger;
    private readonly OpenMeteoOptions _options;
    private readonly ResiliencePipeline _resiliencePipeline;

    public OpenMeteoWeatherService_Legacy(
        HttpClient httpClient,
        ILogger<OpenMeteoWeatherService_Legacy> logger,
        IOptions<OpenMeteoOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        
        // Simplified resilience pipeline for now
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    public async Task<WeatherForecast> GetCurrentWeatherAsync(
        Coordinate location,
        CancellationToken cancellationToken = default)
    {
        var url = BuildCurrentWeatherUrl(location);

        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(async _ =>
            {
                _logger.LogInformation("Calling Open-Meteo current weather API: {Url}", url);
                return await _httpClient.GetAsync(url, cancellationToken);
            }, cancellationToken);

            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OpenMeteoCurrentResponse>(content, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (result?.CurrentWeather == null)
                throw new InvalidOperationException("Invalid response from Open-Meteo API");

            return MapCurrentWeatherToForecast(result, location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Open-Meteo current weather API");
            throw;
        }
    }

    public async Task<WeatherForecast> GetForecastAsync(
        Coordinate location,
        DateTime dateTime,
        CancellationToken cancellationToken = default)
    {
        var hourlyForecasts = await GetHourlyForecastAsync(location, dateTime.Date, dateTime.Date.AddDays(1), cancellationToken);
        
        // Find the forecast closest to the requested time
        return hourlyForecasts
            .OrderBy(f => Math.Abs((f.DateTime - dateTime).TotalMinutes))
            .First();
    }

    public async Task<List<WeatherForecast>> GetHourlyForecastAsync(
        Coordinate location,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var url = BuildForecastUrl(location, startDate, endDate);

        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(async _ =>
            {
                _logger.LogInformation("Calling Open-Meteo forecast API: {Url}", url);
                return await _httpClient.GetAsync(url, cancellationToken);
            }, cancellationToken);

            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OpenMeteoForecastResponse>(content, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (result?.Hourly == null)
                throw new InvalidOperationException("Invalid response from Open-Meteo API");

            return MapHourlyForecastToWeatherForecasts(result, location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Open-Meteo forecast API");
            throw;
        }
    }

    private string BuildCurrentWeatherUrl(Coordinate location)
    {
        return $"{_options.BaseUrl}/v1/forecast?" +
               $"latitude={location.Latitude:F6}&" +
               $"longitude={location.Longitude:F6}&" +
               "current_weather=true&" +
               "hourly=temperature_2m,relative_humidity_2m,precipitation,weather_code,visibility,wind_speed_10m,wind_direction_10m,uv_index&" +
               "timezone=auto";
    }

    private string BuildForecastUrl(Coordinate location, DateTime startDate, DateTime endDate)
    {
        return $"{_options.BaseUrl}/v1/forecast?" +
               $"latitude={location.Latitude:F6}&" +
               $"longitude={location.Longitude:F6}&" +
               "hourly=temperature_2m,relative_humidity_2m,precipitation,weather_code,visibility,wind_speed_10m,wind_direction_10m,uv_index,cloud_cover&" +
               $"start_date={startDate:yyyy-MM-dd}&" +
               $"end_date={endDate:yyyy-MM-dd}&" +
               "timezone=auto";
    }

    private WeatherForecast MapCurrentWeatherToForecast(OpenMeteoCurrentResponse response, Coordinate location)
    {
        var current = response.CurrentWeather;
        var temperatureC = current.Temperature;
        var temperatureF = (temperatureC * 9.0 / 5.0) + 32;
        
        return new WeatherForecast(
            location,
            DateTime.Parse(current.Time),
            temperatureC,
            temperatureF,
            0, // Humidity not available in current weather
            current.WindSpeed,
            current.WindSpeed * 0.621371, // Convert km/h to mph
            current.WindDirection,
            0, // Precipitation not available in current weather
            0,
            MapWeatherCodeToCondition(current.WeatherCode),
            GetWeatherDescription(current.WeatherCode),
            10, // Default visibility
            0, // Cloud cover not available
            0); // UV index not available in current weather
    }

    private List<WeatherForecast> MapHourlyForecastToWeatherForecasts(OpenMeteoForecastResponse response, Coordinate location)
    {
        var forecasts = new List<WeatherForecast>();
        var hourly = response.Hourly;

        for (int i = 0; i < hourly.Time.Length; i++)
        {
            var temperatureC = hourly.Temperature2m[i];
            var temperatureF = (temperatureC * 9.0 / 5.0) + 32;
            var windSpeedKmh = hourly.WindSpeed10m?[i] ?? 0;
            var windSpeedMph = windSpeedKmh * 0.621371;
            var precipitationMm = hourly.Precipitation?[i] ?? 0;
            var precipitationIn = precipitationMm * 0.0393701;

            forecasts.Add(new WeatherForecast(
                location,
                DateTime.Parse(hourly.Time[i]),
                temperatureC,
                temperatureF,
                hourly.RelativeHumidity2m?[i] ?? 0,
                windSpeedKmh,
                windSpeedMph,
                hourly.WindDirection10m?[i] ?? 0,
                precipitationMm,
                precipitationIn,
                MapWeatherCodeToCondition(hourly.WeatherCode?[i] ?? 0),
                GetWeatherDescription(hourly.WeatherCode?[i] ?? 0),
                hourly.Visibility?[i] ?? 10, // Default to 10km if not available
                hourly.CloudCover?[i] ?? 0,
                hourly.UvIndex?[i] ?? 0));
        }

        return forecasts;
    }

    private WeatherCondition MapWeatherCodeToCondition(int weatherCode)
    {
        return weatherCode switch
        {
            0 => WeatherCondition.Clear, // Clear sky
            1 or 2 or 3 => WeatherCondition.Cloudy, // Mainly clear, partly cloudy, overcast
            45 or 48 => WeatherCondition.Fog, // Fog
            51 or 53 or 55 or 56 or 57 => WeatherCondition.Rain, // Drizzle
            61 or 63 or 65 or 66 or 67 => WeatherCondition.Rain, // Rain
            71 or 73 or 75 or 77 => WeatherCondition.Snow, // Snow
            80 or 81 or 82 => WeatherCondition.Rain, // Rain showers
            85 or 86 => WeatherCondition.Snow, // Snow showers
            95 or 96 or 99 => WeatherCondition.Storm, // Thunderstorm
            _ => WeatherCondition.Clear
        };
    }

    private string GetWeatherDescription(int weatherCode)
    {
        return weatherCode switch
        {
            0 => "Clear sky",
            1 => "Mainly clear",
            2 => "Partly cloudy",
            3 => "Overcast",
            45 => "Fog",
            48 => "Depositing rime fog",
            51 => "Light drizzle",
            53 => "Moderate drizzle",
            55 => "Dense drizzle",
            56 => "Light freezing drizzle",
            57 => "Dense freezing drizzle",
            61 => "Slight rain",
            63 => "Moderate rain",
            65 => "Heavy rain",
            66 => "Light freezing rain",
            67 => "Heavy freezing rain",
            71 => "Slight snow fall",
            73 => "Moderate snow fall",
            75 => "Heavy snow fall",
            77 => "Snow grains",
            80 => "Slight rain showers",
            81 => "Moderate rain showers",
            82 => "Violent rain showers",
            85 => "Slight snow showers",
            86 => "Heavy snow showers",
            95 => "Thunderstorm",
            96 => "Thunderstorm with slight hail",
            99 => "Thunderstorm with heavy hail",
            _ => "Unknown"
        };
    }
}

public class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";
    public string BaseUrl { get; set; } = "https://api.open-meteo.com";
}

// Open-Meteo API Response Models
internal record OpenMeteoCurrentResponse(
    OpenMeteoCurrentWeather CurrentWeather);

internal record OpenMeteoCurrentWeather(
    double Temperature,
    double WindSpeed,
    int WindDirection,
    int WeatherCode,
    string Time);

internal record OpenMeteoForecastResponse(
    OpenMeteoHourly Hourly);

internal record OpenMeteoHourly(
    string[] Time,
    double[] Temperature2m,
    int[]? RelativeHumidity2m,
    double[]? Precipitation,
    int[]? WeatherCode,
    double[]? Visibility,
    double[]? WindSpeed10m,
    int[]? WindDirection10m,
    double[]? UvIndex,
    int[]? CloudCover);
