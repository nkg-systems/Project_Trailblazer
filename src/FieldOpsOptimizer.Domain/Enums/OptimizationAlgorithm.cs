namespace FieldOpsOptimizer.Domain.Enums;

/// <summary>
/// Available route optimization algorithms
/// </summary>
public enum OptimizationAlgorithm
{
    /// <summary>
    /// Simple nearest neighbor greedy algorithm
    /// Fast but may not find optimal solutions
    /// </summary>
    NearestNeighbor = 1,

    /// <summary>
    /// 2-opt local search improvement
    /// Good for improving existing routes
    /// </summary>
    TwoOpt = 2,

    /// <summary>
    /// Genetic algorithm with population-based search
    /// More sophisticated but slower
    /// </summary>
    Genetic = 3,

    /// <summary>
    /// Simulated annealing
    /// Good balance between quality and speed
    /// </summary>
    SimulatedAnnealing = 4,

    /// <summary>
    /// Hybrid approach combining multiple algorithms
    /// Best quality but slowest
    /// </summary>
    Hybrid = 5,

    /// <summary>
    /// Christofides algorithm for TSP
    /// Theoretical guarantee for TSP problems
    /// </summary>
    Christofides = 6,

    /// <summary>
    /// Lin-Kernighan heuristic
    /// Advanced local search method
    /// </summary>
    LinKernighan = 7
}
