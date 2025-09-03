using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Api.DTOs;
using System.ComponentModel.DataAnnotations;
using WeatherForecast = FieldOpsOptimizer.Application.Common.Interfaces.WeatherForecast;

namespace FieldOpsOptimizer.Api.Controllers;

/// <summary>
/// Controller for weather-related operations and field work planning
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(
        IWeatherService weatherService,
        ILogger<WeatherController> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
    }

    /// <summary>
    /// Get current weather conditions for a specific location
    /// </summary>
    /// <param name="latitude">Latitude coordinate</param>
    /// <param name="longitude">Longitude coordinate</param>
    /// <returns>Current weather information</returns>
    [HttpGet("current")]
    [ProducesResponseType(typeof(ApiResponse<WeatherForecastDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<WeatherForecastDto>>> GetCurrentWeather(
        [FromQuery] [Required] double latitude,
        [FromQuery] [Required] double longitude)
    {
        try
        {
            var location = new Coordinate(latitude, longitude);
            var weather = await _weatherService.GetCurrentWeatherAsync(location);
            
            var weatherDto = new WeatherForecastDto
            {
                Location = new CoordinateDto { Latitude = weather.Location.Latitude, Longitude = weather.Location.Longitude },
                DateTime = weather.DateTime,
                TemperatureCelsius = weather.TemperatureCelsius,
                TemperatureFahrenheit = weather.TemperatureFahrenheit,
                Humidity = weather.Humidity,
                WindSpeedKmh = weather.WindSpeedKmh,
                WindSpeedMph = weather.WindSpeedMph,
                WindDirection = weather.WindDirection,
                PrecipitationMm = weather.PrecipitationMm,
                PrecipitationIn = weather.PrecipitationIn,
                Condition = weather.Condition,
                Description = weather.Description,
                Visibility = weather.Visibility,
                CloudCover = weather.CloudCover,
                UvIndex = weather.UvIndex,
                IsSuitableForFieldWork = weather.IsSuitableForFieldWork,
                WorkEfficiencyFactor = weather.WorkEfficiencyFactor
            };

            return Ok(new ApiResponse<WeatherForecastDto>
            {
                Success = true,
                Data = weatherDto,
                Message = "Current weather retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current weather for {Latitude}, {Longitude}", latitude, longitude);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "An error occurred while retrieving weather data",
                    Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get weather forecast for a specific location and date range
    /// </summary>
    /// <param name="latitude">Latitude coordinate</param>
    /// <param name="longitude">Longitude coordinate</param>
    /// <param name="startDate">Start date for forecast</param>
    /// <param name="endDate">End date for forecast</param>
    /// <returns>Weather forecast data</returns>
    [HttpGet("forecast")]
    [ProducesResponseType(typeof(ApiResponse<List<WeatherForecastDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<WeatherForecastDto>>>> GetWeatherForecast(
        [FromQuery] [Required] double latitude,
        [FromQuery] [Required] double longitude,
        [FromQuery] [Required] DateTime startDate,
        [FromQuery] [Required] DateTime endDate)
    {
        try
        {
            var location = new Coordinate(latitude, longitude);
            var forecasts = await _weatherService.GetHourlyForecastAsync(location, startDate, endDate);
            
            var forecastDtos = forecasts.Select(f => new WeatherForecastDto
            {
                Location = new CoordinateDto { Latitude = f.Location.Latitude, Longitude = f.Location.Longitude },
                DateTime = f.DateTime,
                TemperatureCelsius = f.TemperatureCelsius,
                TemperatureFahrenheit = f.TemperatureFahrenheit,
                Humidity = f.Humidity,
                WindSpeedKmh = f.WindSpeedKmh,
                WindSpeedMph = f.WindSpeedMph,
                WindDirection = f.WindDirection,
                PrecipitationMm = f.PrecipitationMm,
                PrecipitationIn = f.PrecipitationIn,
                Condition = f.Condition,
                Description = f.Description,
                Visibility = f.Visibility,
                CloudCover = f.CloudCover,
                UvIndex = f.UvIndex,
                IsSuitableForFieldWork = f.IsSuitableForFieldWork,
                WorkEfficiencyFactor = f.WorkEfficiencyFactor
            }).ToList();

            return Ok(new ApiResponse<List<WeatherForecastDto>>
            {
                Success = true,
                Data = forecastDtos,
                Message = $"Retrieved {forecastDtos.Count} forecast entries"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving forecast for {Latitude}, {Longitude}", latitude, longitude);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "An error occurred while retrieving forecast data",
                    Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Check if weather conditions are suitable for field work
    /// </summary>
    /// <param name="latitude">Latitude coordinate</param>
    /// <param name="longitude">Longitude coordinate</param>
    /// <param name="dateTime">Date and time to check (optional, defaults to now)</param>
    /// <returns>Work suitability assessment</returns>
    [HttpGet("work-suitability")]
    [ProducesResponseType(typeof(ApiResponse<WeatherWorkSuitabilityDto>), 200)]
    public async Task<ActionResult<ApiResponse<WeatherWorkSuitabilityDto>>> GetWorkSuitability(
        [FromQuery] [Required] double latitude,
        [FromQuery] [Required] double longitude,
        [FromQuery] DateTime? dateTime = null)
    {
        try
        {
            var location = new Coordinate(latitude, longitude);
            var checkTime = dateTime ?? DateTime.UtcNow;
            
            var suitability = await _weatherService.IsWeatherSuitableForWorkAsync(location, checkTime);
            
            var suitabilityDto = new WeatherWorkSuitabilityDto
            {
                IsSuitable = suitability.IsSuitable,
                Severity = suitability.Severity.ToString(),
                RecommendedAction = suitability.RecommendedAction,
                SafetyConsiderations = suitability.SafetyConsiderations,
                Weather = new WeatherForecastDto
                {
                    Location = new CoordinateDto { Latitude = suitability.WeatherData.Location.Latitude, Longitude = suitability.WeatherData.Location.Longitude },
                    DateTime = suitability.WeatherData.DateTime,
                    TemperatureCelsius = suitability.WeatherData.TemperatureCelsius,
                    TemperatureFahrenheit = suitability.WeatherData.TemperatureFahrenheit,
                    Humidity = suitability.WeatherData.Humidity,
                    WindSpeedKmh = suitability.WeatherData.WindSpeedKmh,
                    WindSpeedMph = suitability.WeatherData.WindSpeedMph,
                    WindDirection = suitability.WeatherData.WindDirection,
                    PrecipitationMm = suitability.WeatherData.PrecipitationMm,
                    PrecipitationIn = suitability.WeatherData.PrecipitationIn,
                    Condition = suitability.WeatherData.Condition,
                    Description = suitability.WeatherData.Description,
                    Visibility = suitability.WeatherData.Visibility,
                    CloudCover = suitability.WeatherData.CloudCover,
                    UvIndex = suitability.WeatherData.UvIndex,
                    IsSuitableForFieldWork = suitability.WeatherData.IsSuitableForFieldWork,
                    WorkEfficiencyFactor = suitability.WeatherData.WorkEfficiencyFactor
                }
            };

            return Ok(new ApiResponse<WeatherWorkSuitabilityDto>
            {
                Success = true,
                Data = suitabilityDto,
                Message = suitability.IsSuitable ? "Weather is suitable for field work" : "Weather may not be suitable for field work"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking work suitability for {Latitude}, {Longitude}", latitude, longitude);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "An error occurred while checking weather suitability",
                    Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get weather-based job scheduling recommendation
    /// </summary>
    /// <param name="request">Job scheduling request details</param>
    /// <returns>Weather-based scheduling recommendation</returns>
    [HttpPost("job-recommendation")]
    [ProducesResponseType(typeof(ApiResponse<JobWeatherRecommendationDto>), 200)]
    public async Task<ActionResult<ApiResponse<JobWeatherRecommendationDto>>> GetJobRecommendation(
        [FromBody] JobSchedulingRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid request data",
                    Errors = ModelState.SelectMany(x => x.Value?.Errors ?? new List<Microsoft.AspNetCore.Mvc.ModelBinding.ModelError>())
                                      .Select(e => e.ErrorMessage)
                                      .ToList()
                });
            }

            var location = new Coordinate(request.Latitude, request.Longitude);
            var recommendation = await _weatherService.GetJobSchedulingRecommendationAsync(
                location,
                request.ProposedDateTime,
                TimeSpan.FromHours(request.EstimatedDurationHours),
                request.Priority
            );

            var recommendationDto = new JobWeatherRecommendationDto
            {
                IsRecommended = recommendation.IsRecommended,
                Recommendation = recommendation.Recommendation,
                ConfidenceLevel = recommendation.ConfidenceLevel.ToString(),
                Considerations = recommendation.Considerations,
                AlternativeTime = recommendation.AlternativeTime,
                CurrentWeather = MapWeatherForecastToDto(recommendation.CurrentWeather),
                ForecastAtTime = recommendation.ForecastAtTime != null ? MapWeatherForecastToDto(recommendation.ForecastAtTime) : null
            };

            return Ok(new ApiResponse<JobWeatherRecommendationDto>
            {
                Success = true,
                Data = recommendationDto,
                Message = recommendation.IsRecommended ? "Job timing is recommended" : "Consider alternative timing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job recommendation");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "An error occurred while generating job recommendation",
                    Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get weather data for multiple locations
    /// </summary>
    /// <param name="request">Batch weather request</param>
    /// <returns>Weather data for all requested locations</returns>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(ApiResponse<List<LocationWeatherDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<LocationWeatherDto>>>> GetWeatherForLocations(
        [FromBody] BatchWeatherRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid request data",
                    Errors = ModelState.SelectMany(x => x.Value?.Errors ?? new List<Microsoft.AspNetCore.Mvc.ModelBinding.ModelError>())
                                      .Select(e => e.ErrorMessage)
                                      .ToList()
                });
            }

            var coordinates = request.Locations.Select(l => new Coordinate(l.Latitude, l.Longitude)).ToList();
            var weatherData = await _weatherService.GetWeatherForLocationsAsync(coordinates);

            var results = weatherData.Select(kvp => new LocationWeatherDto
            {
                Location = new CoordinateDto { Latitude = kvp.Key.Latitude, Longitude = kvp.Key.Longitude },
                Weather = kvp.Value != null ? MapWeatherForecastToDto(kvp.Value) : null,
                IsAvailable = kvp.Value != null
            }).ToList();

            return Ok(new ApiResponse<List<LocationWeatherDto>>
            {
                Success = true,
                Data = results,
                Message = $"Retrieved weather data for {results.Count(r => r.IsAvailable)} of {results.Count} locations"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving batch weather data");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "An error occurred while retrieving batch weather data",
                    Errors = new List<string> { ex.Message }
            });
        }
    }

    /// <summary>
    /// Get optimal weather windows for job planning
    /// </summary>
    /// <param name="request">Optimal weather windows request</param>
    /// <returns>List of optimal weather windows</returns>
    [HttpPost("optimal-windows")]
    [ProducesResponseType(typeof(ApiResponse<List<OptimalWeatherWindowDto>>), 200)]
    public async Task<ActionResult<ApiResponse<List<OptimalWeatherWindowDto>>>> GetOptimalWeatherWindows(
        [FromBody] OptimalWeatherWindowsRequestDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid request data",
                    Errors = ModelState.SelectMany(x => x.Value?.Errors ?? new List<Microsoft.AspNetCore.Mvc.ModelBinding.ModelError>())
                                      .Select(e => e.ErrorMessage)
                                      .ToList()
                });
            }

            var jobRequests = request.JobRequests.Select(jr => new JobLocationRequest
            {
                JobId = jr.JobId,
                Location = new Coordinate(jr.Latitude, jr.Longitude),
                PreferredStartTime = jr.PreferredStartTime,
                EstimatedDuration = TimeSpan.FromHours(jr.EstimatedDurationHours),
                Priority = jr.Priority,
                WeatherConstraints = jr.WeatherConstraints
            }).ToList();

            var windows = await _weatherService.GetOptimalWeatherWindowsAsync(
                jobRequests,
                request.StartDate,
                request.EndDate
            );

            var windowDtos = windows.Select(w => new OptimalWeatherWindowDto
            {
                Location = new CoordinateDto { Latitude = w.Location.Latitude, Longitude = w.Location.Longitude },
                StartTime = w.StartTime,
                EndTime = w.EndTime,
                OptimalityScore = w.OptimalityScore,
                ExpectedCondition = w.ExpectedCondition,
                Reasoning = w.Reasoning,
                SuitableJobIds = w.SuitableJobIds
            }).ToList();

            return Ok(new ApiResponse<List<OptimalWeatherWindowDto>>
            {
                Success = true,
                Data = windowDtos,
                Message = $"Found {windowDtos.Count} optimal weather windows"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding optimal weather windows");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "An error occurred while finding optimal weather windows",
                    Errors = new List<string> { ex.Message }
            });
        }
    }

    #region Private Helper Methods

    private static WeatherForecastDto MapWeatherForecastToDto(WeatherForecast weather)
    {
        return new WeatherForecastDto
        {
            Location = new CoordinateDto { Latitude = weather.Location.Latitude, Longitude = weather.Location.Longitude },
            DateTime = weather.DateTime,
            TemperatureCelsius = weather.TemperatureCelsius,
            TemperatureFahrenheit = weather.TemperatureFahrenheit,
            Humidity = weather.Humidity,
            WindSpeedKmh = weather.WindSpeedKmh,
            WindSpeedMph = weather.WindSpeedMph,
            WindDirection = weather.WindDirection,
            PrecipitationMm = weather.PrecipitationMm,
            PrecipitationIn = weather.PrecipitationIn,
            Condition = weather.Condition,
            Description = weather.Description,
            Visibility = weather.Visibility,
            CloudCover = weather.CloudCover,
            UvIndex = weather.UvIndex,
            IsSuitableForFieldWork = weather.IsSuitableForFieldWork,
            WorkEfficiencyFactor = weather.WorkEfficiencyFactor
        };
    }

    #endregion
}
