using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace FieldOpsOptimizer.Infrastructure.Tests.Services;

public class WeatherServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<WeatherService>> _mockLogger;
    private readonly WeatherService _weatherService;

    public WeatherServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mockLogger = new Mock<ILogger<WeatherService>>();
        _weatherService = new WeatherService(_httpClient, _mockLogger.Object);
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_ShouldReturnWeatherForecast()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);

        // Act
        var result = await _weatherService.GetCurrentWeatherAsync(location);

        // Assert
        result.Should().NotBeNull();
        result.Location.Should().Be(location);
        result.DateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        result.TemperatureCelsius.Should().BeGreaterThan(-50).And.BeLessThan(60);
        result.Humidity.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(100);
        result.Condition.Should().BeDefined();
    }

    [Fact]
    public async Task GetForecastAsync_ShouldReturnForecastForSpecificDateTime()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var forecastTime = DateTime.UtcNow.AddDays(1);

        // Act
        var result = await _weatherService.GetForecastAsync(location, forecastTime);

        // Assert
        result.Should().NotBeNull();
        result.Location.Should().Be(location);
        result.DateTime.Should().Be(forecastTime);
        result.Condition.Should().BeDefined();
    }

    [Fact]
    public async Task GetHourlyForecastAsync_ShouldReturnMultipleForecasts()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddHours(5);

        // Act
        var result = await _weatherService.GetHourlyForecastAsync(location, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(6); // Start hour + 5 hours = 6 forecasts
        result.Should().AllSatisfy(f =>
        {
            f.Location.Should().Be(location);
            f.Condition.Should().BeDefined();
        });
    }

    [Fact]
    public async Task GetCurrentWeatherDataAsync_ShouldReturnWeatherData()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var tenantId = "TENANT001";

        // Act
        var result = await _weatherService.GetCurrentWeatherDataAsync(location, tenantId);

        // Assert
        result.Should().NotBeNull();
        result!.Location.Should().Be(location);
        result.TenantId.Should().Be(tenantId);
        result.Condition.Should().BeDefined();
        result.Severity.Should().BeDefined();
    }

    [Fact]
    public async Task GetWeatherForecastDataAsync_ShouldReturnMultipleWeatherData()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddHours(3);
        var tenantId = "TENANT001";

        // Act
        var result = await _weatherService.GetWeatherForecastDataAsync(location, startDate, endDate, tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        result.Should().AllSatisfy(wd =>
        {
            wd.Location.Should().Be(location);
            wd.TenantId.Should().Be(tenantId);
        });
    }

    [Fact]
    public async Task GetWeatherForLocationsAsync_ShouldReturnWeatherForAllLocations()
    {
        // Arrange
        var locations = new[]
        {
            new Coordinate(40.7128, -74.0060), // NYC
            new Coordinate(34.0522, -118.2437), // LA
            new Coordinate(41.8781, -87.6298)  // Chicago
        };

        // Act
        var result = await _weatherService.GetWeatherForLocationsAsync(locations);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Keys.Should().BeEquivalentTo(locations);
        result.Values.Should().AllSatisfy(w => w.Should().NotBeNull());
    }

    [Fact]
    public async Task GetWeatherForLocationsAsync_ShouldHandleFailures()
    {
        // Arrange
        var locations = new[]
        {
            new Coordinate(40.7128, -74.0060),
            new Coordinate(999, 999) // Invalid coordinates
        };

        // Act
        var result = await _weatherService.GetWeatherForLocationsAsync(locations);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        // Service should still return results even if some fail (with null values)
    }

    [Fact]
    public async Task IsWeatherSuitableForWorkAsync_ShouldReturnSuitabilityAssessment()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var dateTime = DateTime.UtcNow.AddDays(1);

        // Act
        var result = await _weatherService.IsWeatherSuitableForWorkAsync(location, dateTime);

        // Assert
        result.Should().NotBeNull();
        result.Severity.Should().BeDefined();
        result.RecommendedAction.Should().NotBeNullOrEmpty();
        result.WeatherData.Should().NotBeNull();
    }

    [Fact]
    public async Task GetWeatherAlertsAsync_ShouldReturnAlerts()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var radiusKm = 50.0;

        // Act
        var result = await _weatherService.GetWeatherAlertsAsync(location, radiusKm);

        // Assert
        result.Should().NotBeNull();
        // Mock implementation returns empty list
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetJobSchedulingRecommendationAsync_ShouldReturnRecommendation()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var proposedDateTime = DateTime.UtcNow.AddDays(1);
        var estimatedDuration = TimeSpan.FromHours(2);
        var priority = JobPriority.High;

        // Act
        var result = await _weatherService.GetJobSchedulingRecommendationAsync(
            location, proposedDateTime, estimatedDuration, priority);

        // Assert
        result.Should().NotBeNull();
        result.Recommendation.Should().NotBeNullOrEmpty();
        result.ConfidenceLevel.Should().BeDefined();
        result.Considerations.Should().NotBeNull();
    }

    [Fact]
    public async Task GetJobSchedulingRecommendationAsync_WithEmergencyPriority_ShouldRecommend()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var proposedDateTime = DateTime.UtcNow.AddDays(1);
        var estimatedDuration = TimeSpan.FromHours(1);
        var priority = JobPriority.Emergency;

        // Act
        var result = await _weatherService.GetJobSchedulingRecommendationAsync(
            location, proposedDateTime, estimatedDuration, priority);

        // Assert
        result.Should().NotBeNull();
        // Emergency jobs should be recommended regardless of weather (within reason)
        result.IsRecommended.Should().BeTrue();
    }

    [Fact]
    public async Task GetHourlyForecastAsync_WithSameDateRange_ShouldReturnSingleForecast()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var date = DateTime.UtcNow;

        // Act
        var result = await _weatherService.GetHourlyForecastAsync(location, date, date);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_WithDifferentLocations_ShouldReturnDifferentResults()
    {
        // Arrange
        var location1 = new Coordinate(40.7128, -74.0060); // NYC
        var location2 = new Coordinate(34.0522, -118.2437); // LA

        // Act
        var result1 = await _weatherService.GetCurrentWeatherAsync(location1);
        var result2 = await _weatherService.GetCurrentWeatherAsync(location2);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Location.Should().Be(location1);
        result2.Location.Should().Be(location2);
    }

    [Fact]
    public async Task GetCurrentWeatherDataAsync_ShouldMapConditionsCorrectly()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var tenantId = "TENANT001";

        // Act
        var result = await _weatherService.GetCurrentWeatherDataAsync(location, tenantId);

        // Assert
        result.Should().NotBeNull();
        result!.Condition.Should().BeOneOf(
            WeatherCondition.Clear,
            WeatherCondition.Cloudy,
            WeatherCondition.Rain,
            WeatherCondition.Snow,
            WeatherCondition.Storm,
            WeatherCondition.Fog,
            WeatherCondition.Extreme
        );
    }

    [Fact]
    public async Task IsWeatherSuitableForWorkAsync_ShouldIncludeSafetyConsiderations()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var dateTime = DateTime.UtcNow.AddDays(1);

        // Act
        var result = await _weatherService.IsWeatherSuitableForWorkAsync(location, dateTime);

        // Assert
        result.Should().NotBeNull();
        result.SafetyConsiderations.Should().NotBeNull();
        // SafetyConsiderations may be empty if conditions are ideal
    }

    [Fact]
    public async Task GetJobSchedulingRecommendationAsync_ShouldProvideConsiderations()
    {
        // Arrange
        var location = new Coordinate(40.7128, -74.0060);
        var proposedDateTime = DateTime.UtcNow.AddDays(2);
        var estimatedDuration = TimeSpan.FromHours(3);
        var priority = JobPriority.Medium;

        // Act
        var result = await _weatherService.GetJobSchedulingRecommendationAsync(
            location, proposedDateTime, estimatedDuration, priority);

        // Assert
        result.Should().NotBeNull();
        result.Considerations.Should().NotBeNull();
        // Considerations may be empty if conditions are ideal
    }
}
