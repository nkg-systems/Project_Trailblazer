using FieldOpsOptimizer.Domain.ValueObjects;
using FieldOpsOptimizer.Domain.Enums;

namespace FieldOpsOptimizer.Application.Common.Interfaces;

public interface IWeatherService
{
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
