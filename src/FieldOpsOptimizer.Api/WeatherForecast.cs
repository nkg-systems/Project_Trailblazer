using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Api.DTOs;

namespace FieldOpsOptimizer.Api;

public class WeatherForecast
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

    // Legacy compatibility properties
    public DateOnly Date { get => DateOnly.FromDateTime(DateTime); set => DateTime = value.ToDateTime(TimeOnly.MinValue); }
    public int TemperatureC { get => (int)TemperatureCelsius; set => TemperatureCelsius = value; }
    public int TemperatureF { get => (int)TemperatureFahrenheit; set => TemperatureFahrenheit = value; }
    public string? Summary { get => Description; set => Description = value ?? string.Empty; }
}
