using FieldOpsOptimizer.Domain.Enums;
using FieldOpsOptimizer.Infrastructure.Optimization;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOpsOptimizer.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering route optimization services
/// </summary>
public static class OptimizationServiceExtensions
{
    /// <summary>
    /// Adds route optimization services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddRouteOptimization(this IServiceCollection services)
    {
        // Register optimization algorithms
        services.AddScoped<NearestNeighborOptimizer>();
        services.AddScoped<TwoOptOptimizer>();
        services.AddScoped<GeneticOptimizer>();

        // Register constraint validator
        services.AddScoped<IRouteConstraintValidator, RouteConstraintValidator>();

        // Register distance matrix service
        services.AddScoped<IDistanceMatrixService, DistanceMatrixService>();

        // Register optimization service
        services.AddScoped<IRouteOptimizationService, RouteOptimizationService>();

        return services;
    }

    /// <summary>
    /// Adds route optimization services with custom configuration
    /// </summary>
    public static IServiceCollection AddRouteOptimization(
        this IServiceCollection services,
        Action<OptimizationOptions> configureOptions)
    {
        // Configure options
        services.Configure(configureOptions);

        // Add optimization services
        return services.AddRouteOptimization();
    }
}

/// <summary>
/// Configuration options for route optimization
/// </summary>
public class OptimizationOptions
{
    /// <summary>
    /// Default optimization algorithm to use
    /// </summary>
    public OptimizationAlgorithm DefaultAlgorithm { get; set; } = OptimizationAlgorithm.TwoOpt;

    /// <summary>
    /// Maximum optimization time in seconds
    /// </summary>
    public int MaxOptimizationTimeSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to validate technician skills by default
    /// </summary>
    public bool ValidateSkillsByDefault { get; set; } = true;

    /// <summary>
    /// Whether to respect time windows by default
    /// </summary>
    public bool RespectTimeWindowsByDefault { get; set; } = true;

    /// <summary>
    /// Default cost per kilometer for cost calculations
    /// </summary>
    public decimal DefaultCostPerKm { get; set; } = 0.50m;

    /// <summary>
    /// Default cost per hour for cost calculations
    /// </summary>
    public decimal DefaultCostPerHour { get; set; } = 25.0m;

    /// <summary>
    /// Default average speed for distance to time conversions (km/h)
    /// </summary>
    public double DefaultAverageSpeedKmh { get; set; } = 40.0;

    /// <summary>
    /// Genetic algorithm specific options
    /// </summary>
    public GeneticAlgorithmOptions Genetic { get; set; } = new();

    /// <summary>
    /// Two-opt algorithm specific options
    /// </summary>
    public TwoOptOptions TwoOpt { get; set; } = new();
}

/// <summary>
/// Configuration options for genetic algorithm
/// </summary>
public class GeneticAlgorithmOptions
{
    /// <summary>
    /// Population size for genetic algorithm
    /// </summary>
    public int PopulationSize { get; set; } = 50;

    /// <summary>
    /// Mutation rate (0.0 to 1.0)
    /// </summary>
    public double MutationRate { get; set; } = 0.02;

    /// <summary>
    /// Crossover rate (0.0 to 1.0)
    /// </summary>
    public double CrossoverRate { get; set; } = 0.8;

    /// <summary>
    /// Elite size (number of best individuals to keep)
    /// </summary>
    public int EliteSize { get; set; } = 5;

    /// <summary>
    /// Maximum number of generations
    /// </summary>
    public int MaxGenerations { get; set; } = 100;

    /// <summary>
    /// Tournament size for selection
    /// </summary>
    public int TournamentSize { get; set; } = 3;
}

/// <summary>
/// Configuration options for 2-opt algorithm
/// </summary>
public class TwoOptOptions
{
    /// <summary>
    /// Maximum number of iterations
    /// </summary>
    public int MaxIterations { get; set; } = 1000;

    /// <summary>
    /// Whether to use random restarts
    /// </summary>
    public bool UseRandomRestarts { get; set; } = false;

    /// <summary>
    /// Number of random restarts if enabled
    /// </summary>
    public int RandomRestarts { get; set; } = 3;
}
