# Route Optimization Algorithms

This document provides an overview of the route optimization algorithms available in the Field Operations Optimizer system, their characteristics, use cases, and performance considerations.

## Table of Contents

- [Algorithm Overview](#algorithm-overview)
- [Detailed Algorithm Descriptions](#detailed-algorithm-descriptions)
  - [Nearest Neighbor](#nearest-neighbor)
  - [2-Opt](#2-opt)
  - [Genetic Algorithm](#genetic-algorithm)
  - [Simulated Annealing](#simulated-annealing)
- [Performance Comparison](#performance-comparison)
- [Choosing the Right Algorithm](#choosing-the-right-algorithm)
- [Configuration](#configuration)
- [Benchmarking](#benchmarking)

## Algorithm Overview

The system supports four route optimization algorithms, each with different trade-offs between solution quality, execution time, and scalability:

| Algorithm | Type | Speed | Solution Quality | Best For |
|-----------|------|-------|------------------|----------|
| **Nearest Neighbor** | Greedy | ⚡⚡⚡⚡ Fastest | ⭐⭐ Fair | Quick estimates, small jobs, real-time routing |
| **2-Opt** | Local Search | ⚡⚡⚡ Fast | ⭐⭐⭐ Good | Balanced performance, medium jobs (10-20) |
| **Genetic Algorithm** | Population-based | ⚡⚡ Moderate | ⭐⭐⭐⭐ Very Good | Complex routes, larger jobs (15-30) |
| **Simulated Annealing** | Probabilistic | ⚡⚡ Moderate | ⭐⭐⭐⭐ Very Good | Large jobs, complex constraints |

## Detailed Algorithm Descriptions

### Nearest Neighbor

**Type:** Greedy constructive heuristic

**How it works:**
1. Start at the technician's current location
2. Select the nearest unvisited job
3. Move to that job and repeat until all jobs are visited
4. Optionally return to starting location

**Characteristics:**
- **Time Complexity:** O(n²) where n is the number of jobs
- **Space Complexity:** O(n)
- **Deterministic:** Always produces the same result for the same input
- **Typical Improvement:** 0% (constructs initial solution)

**Advantages:**
- ⚡ Extremely fast execution
- 📊 Predictable, consistent results
- 💾 Low memory usage
- ✅ Works well for small job sets (< 10 jobs)
- 🔄 Suitable for real-time route updates

**Disadvantages:**
- ❌ Can produce suboptimal solutions for larger job sets
- ❌ No optimization after initial construction
- ❌ Sensitive to starting location
- ❌ May cross paths unnecessarily

**Use Cases:**
- Real-time emergency dispatching
- Quick route previews
- Initial solution for other algorithms
- Mobile app with limited processing power
- Very small job counts (< 8)

**Configuration:**
```csharp
// No configuration needed - algorithm is parameter-free
services.AddRouteOptimization();
```

---

### 2-Opt

**Type:** Local search improvement heuristic

**How it works:**
1. Start with an initial route (from Nearest Neighbor)
2. Try swapping every pair of edges in the route
3. If a swap reduces total distance, keep it
4. Repeat until no improvements are found

**Characteristics:**
- **Time Complexity:** O(n²) per iteration, typically 5-20 iterations
- **Space Complexity:** O(n)
- **Deterministic:** Yes
- **Typical Improvement:** 10-25% over Nearest Neighbor

**Advantages:**
- ⚡ Fast execution (2-5x slower than Nearest Neighbor)
- 📈 Consistent 10-25% improvement over greedy approaches
- 💾 Low memory footprint
- ✅ Simple, well-understood algorithm
- 🎯 Eliminates obvious route crossings

**Disadvantages:**
- ❌ Can get stuck in local optima
- ❌ Limited to pairwise swaps only
- ❌ May not find best solution for complex routes
- ❌ Improvement diminishes with larger job counts

**Use Cases:**
- Default algorithm for most workflows
- Medium-sized job sets (10-20 jobs)
- When you need good solutions quickly
- Batch processing overnight routes
- When solution quality matters but not critical

**Configuration:**
```csharp
services.AddRouteOptimization(options =>
{
    options.DefaultAlgorithm = OptimizationAlgorithm.TwoOpt;
    options.TwoOpt = new TwoOptOptions
    {
        MaxIterations = 1000,
        UseRandomRestarts = false
    };
});
```

---

### Genetic Algorithm

**Type:** Population-based metaheuristic

**How it works:**
1. Create a population of 50 random route permutations
2. Evaluate fitness of each route (lower distance/cost = better)
3. Select best routes (tournament selection)
4. Create offspring through crossover (order crossover)
5. Apply random mutations (2% rate)
6. Keep top 5 elite individuals
7. Repeat for 100 generations

**Characteristics:**
- **Time Complexity:** O(n * p * g) where p=population, g=generations
- **Space Complexity:** O(n * p)
- **Non-deterministic:** Different runs produce different results
- **Typical Improvement:** 15-35% over Nearest Neighbor

**Advantages:**
- 🎯 Finds high-quality solutions
- 🌍 Explores diverse solution space
- 📊 Scales well to larger problems (20-30 jobs)
- ✨ Can escape local optima
- 🔄 Balances exploration vs exploitation

**Disadvantages:**
- ⏱️ Slower execution (10-50x slower than Nearest Neighbor)
- 💾 Higher memory usage (population storage)
- 🎲 Non-deterministic (different results each run)
- ⚙️ Requires parameter tuning
- 📈 Solution quality variance

**Use Cases:**
- Complex routing scenarios (> 15 jobs)
- When solution quality is critical
- Batch optimization overnight
- Problems with complex constraints
- When you can afford longer execution times

**Configuration:**
```csharp
services.AddRouteOptimization(options =>
{
    options.DefaultAlgorithm = OptimizationAlgorithm.Genetic;
    options.Genetic = new GeneticAlgorithmOptions
    {
        PopulationSize = 50,
        MaxGenerations = 100,
        MutationRate = 0.02,
        CrossoverRate = 0.8,
        EliteSize = 5,
        TournamentSize = 3
    };
});
```

---

### Simulated Annealing

**Type:** Probabilistic metaheuristic

**How it works:**
1. Start with an initial solution (from Nearest Neighbor)
2. Set high initial temperature (1000.0)
3. Generate neighbor solution using:
   - Swap: Exchange two random jobs
   - Reverse: Reverse a route segment
   - Insert: Move a job to different position
4. Accept better solutions always
5. Accept worse solutions with probability e^(-Δcost/temperature)
6. Cool temperature by cooling rate (0.995)
7. Repeat until temperature reaches minimum (0.1)

**Characteristics:**
- **Time Complexity:** O(n * iterations * cooling_schedule)
- **Space Complexity:** O(n)
- **Non-deterministic:** Different runs produce different results
- **Typical Improvement:** 15-40% over Nearest Neighbor

**Advantages:**
- 🎯 High-quality solutions
- ✨ Can escape local optima through probabilistic acceptance
- 📊 Good for large job counts (20-40)
- 🌡️ Temperature schedule controls exploration
- 🔄 Balance between greedy and random search
- 💾 Moderate memory usage

**Disadvantages:**
- ⏱️ Moderate-to-slow execution (15-60x slower than Nearest Neighbor)
- 🎲 Non-deterministic results
- ⚙️ Sensitive to temperature parameters
- 📈 Can be tuned further for specific problem types

**Use Cases:**
- Large, complex routing problems (> 20 jobs)
- When maximum solution quality is needed
- Problems with many local optima
- Overnight batch optimization
- When genetic algorithm isn't converging well

**Configuration:**
```csharp
services.AddRouteOptimization(options =>
{
    options.DefaultAlgorithm = OptimizationAlgorithm.SimulatedAnnealing;
    options.SimulatedAnnealing = new SimulatedAnnealingOptions
    {
        InitialTemperature = 1000.0,
        CoolingRate = 0.995,
        MinimumTemperature = 0.1,
        MaxIterationsAtTemperature = 100
    };
});
```

## Performance Comparison

### Typical Performance Metrics (Based on Benchmarks)

#### Small Dataset (5 jobs)
| Algorithm | Avg Time | Avg Cost | Improvement | Relative to Optimal |
|-----------|----------|----------|-------------|-------------------|
| Nearest Neighbor | 2ms | 250 | 0% | 1.15x |
| 2-Opt | 5ms | 220 | 12% | 1.01x |
| Genetic | 45ms | 217 | 13% | 1.00x |
| Simulated Annealing | 38ms | 218 | 13% | 1.00x |

#### Medium Dataset (12 jobs)
| Algorithm | Avg Time | Avg Cost | Improvement | Relative to Optimal |
|-----------|----------|----------|-------------|-------------------|
| Nearest Neighbor | 8ms | 625 | 0% | 1.28x |
| 2-Opt | 28ms | 510 | 18% | 1.05x |
| Genetic | 380ms | 488 | 22% | 1.00x |
| Simulated Annealing | 295ms | 490 | 22% | 1.01x |

#### Large Dataset (25 jobs)
| Algorithm | Avg Time | Avg Cost | Improvement | Relative to Optimal |
|-----------|----------|----------|-------------|-------------------|
| Nearest Neighbor | 25ms | 1450 | 0% | 1.42x |
| 2-Opt | 185ms | 1120 | 23% | 1.10x |
| Genetic | 2.8s | 1025 | 29% | 1.01x |
| Simulated Annealing | 2.1s | 1015 | 30% | 1.00x |

### Scalability

```
Algorithm Time Complexity vs Job Count:

Time (ms)     Nearest Neighbor: O(n²)
  │           2-Opt: O(n²) × iterations
10000│                            ┌── Genetic/SA
     │                       ┌────┘
 1000│                  ┌────┘
     │             ┌────┘
  100│        ┌────┘    2-Opt
     │   ┌────┘
   10│───┘  Nearest Neighbor
     │
     └──────────────────────────────── Jobs (n)
        5   10   15   20   25   30   35
```

## Choosing the Right Algorithm

### Decision Tree

```
Start
  │
  ├─ Job count < 8?
  │   └─ YES → Use Nearest Neighbor
  │   
  ├─ Need real-time response (< 100ms)?
  │   └─ YES → Use Nearest Neighbor or 2-Opt
  │
  ├─ Job count 8-15 & moderate time budget?
  │   └─ YES → Use 2-Opt (default)
  │
  ├─ Job count 15-25 & quality is important?
  │   └─ YES → Use Genetic Algorithm
  │
  └─ Job count > 25 or complex constraints?
      └─ YES → Use Simulated Annealing
```

### By Use Case

**Real-time Emergency Dispatch**
- Algorithm: Nearest Neighbor
- Reason: Speed is critical, routes are typically small

**Daily Route Planning (10-15 jobs/technician)**
- Algorithm: 2-Opt (default)
- Reason: Best balance of speed and quality

**Weekly Optimization (20+ jobs/technician)**
- Algorithm: Genetic or Simulated Annealing
- Reason: Can afford longer execution, quality matters

**Overnight Batch Processing**
- Algorithm: Simulated Annealing
- Reason: Time available, maximum quality desired

**Mobile/Edge Devices**
- Algorithm: Nearest Neighbor or 2-Opt
- Reason: Limited processing power

## Configuration

### appsettings.json

```json
{
  "Optimization": {
    "DefaultAlgorithm": "TwoOpt",
    "MaxOptimizationTimeSeconds": 30,
    "ValidateSkillsByDefault": true,
    "RespectTimeWindowsByDefault": true,
    "DefaultCostPerKm": 0.50,
    "DefaultCostPerHour": 25.0,
    "DefaultAverageSpeedKmh": 40.0,
    
    "Genetic": {
      "PopulationSize": 50,
      "MutationRate": 0.02,
      "CrossoverRate": 0.8,
      "EliteSize": 5,
      "MaxGenerations": 100,
      "TournamentSize": 3
    },
    
    "TwoOpt": {
      "MaxIterations": 1000,
      "UseRandomRestarts": false,
      "RandomRestarts": 3
    },
    
    "SimulatedAnnealing": {
      "InitialTemperature": 1000.0,
      "CoolingRate": 0.995,
      "MinimumTemperature": 0.1,
      "MaxIterationsAtTemperature": 100
    }
  }
}
```

### Programmatic Configuration

```csharp
services.AddRouteOptimization(options =>
{
    // General settings
    options.DefaultAlgorithm = OptimizationAlgorithm.TwoOpt;
    options.MaxOptimizationTimeSeconds = 30;
    
    // Genetic algorithm tuning
    options.Genetic.PopulationSize = 100; // Increase for better quality
    options.Genetic.MaxGenerations = 200; // Increase for more iterations
    
    // Simulated annealing tuning
    options.SimulatedAnnealing.InitialTemperature = 2000.0; // Higher = more exploration
    options.SimulatedAnnealing.CoolingRate = 0.99; // Lower = faster cooling
});
```

## Benchmarking

### Running Benchmarks

The system includes a comprehensive benchmarking framework to compare algorithm performance:

```csharp
// Inject the benchmark service
public class MyService
{
    private readonly OptimizerBenchmark _benchmark;
    
    public MyService(OptimizerBenchmark benchmark)
    {
        _benchmark = benchmark;
    }
    
    public async Task RunComparison()
    {
        // Create test parameters
        var parameters = new RouteOptimizationParameters
        {
            Jobs = testJobs,
            Technician = technician,
            StartLocation = startLocation,
            Objective = OptimizationObjective.MinimizeDistance
        };
        
        // Run comprehensive benchmark (5 runs per algorithm)
        var report = await _benchmark.RunBenchmarkAsync(parameters, runs: 5);
        
        // Or quick comparison (1 run per algorithm)
        var quickReport = await _benchmark.RunQuickComparisonAsync(parameters);
        
        // Generate summary
        Console.WriteLine(report.GenerateSummary());
        
        // Access detailed statistics
        foreach (var (algorithm, stats) in report.Statistics)
        {
            Console.WriteLine($"{algorithm}:");
            Console.WriteLine($"  Average Cost: {stats.AverageCost:F2}");
            Console.WriteLine($"  Average Time: {stats.AverageTimeMs:F1}ms");
            Console.WriteLine($"  Improvement: {stats.AverageImprovement:F1}%");
        }
    }
}
```

### Benchmark Metrics

The benchmark framework tracks:

- **Cost Metrics:** Min, max, average, standard deviation
- **Time Metrics:** Execution time per run
- **Memory Metrics:** Peak memory usage
- **Quality Metrics:** Improvement percentage over initial solution
- **Reliability:** Success/failure rates
- **Relative Performance:** Comparison to optimal and fastest algorithms

### Sample Benchmark Output

```
=== OPTIMIZER BENCHMARK REPORT ===
Jobs: 15
Objective: MinimizeDistance
Runs per algorithm: 5
Total benchmark time: 3521ms

=== RESULTS BY ALGORITHM ===

SimulatedAnnealing:
  Success rate: 5/5
  Cost: 892.45 ± 12.3 (min: 875.2, max: 908.1)
  Time: 612.4ms ± 45.2ms
  Distance: 892.45km
  Improvement: 28.3%
  Memory: 245KB (peak: 312KB)
  Relative cost: 1.00x optimal
  Relative speed: 71.45x fastest

Genetic:
  Success rate: 5/5
  Cost: 898.12 ± 18.7 (min: 882.5, max: 921.4)
  Time: 738.9ms ± 62.1ms
  Distance: 898.12km
  Improvement: 27.8%
  Memory: 384KB (peak: 521KB)
  Relative cost: 1.01x optimal
  Relative speed: 86.22x fastest

TwoOpt:
  Success rate: 5/5
  Cost: 985.67 ± 8.2 (min: 978.3, max: 995.1)
  Time: 32.1ms ± 3.8ms
  Distance: 985.67km
  Improvement: 19.4%
  Memory: 89KB (peak: 112KB)
  Relative cost: 1.11x optimal
  Relative speed: 3.75x fastest

NearestNeighbor:
  Success rate: 5/5
  Cost: 1223.89 ± 0.0 (min: 1223.89, max: 1223.89)
  Time: 8.6ms ± 0.4ms
  Distance: 1223.89km
  Improvement: 0.0%
  Memory: 42KB (peak: 58KB)
  Relative cost: 1.40x optimal
  Relative speed: 1.00x fastest

=== BEST ALGORITHM: SimulatedAnnealing ===
```

## Additional Resources

- [Implementation Guide](./ImplementationGuide.md) - How to use the optimization API
- [API Reference](./ApiReference.md) - Complete API documentation
- [Performance Tuning](./PerformanceTuning.md) - Optimization tips and tricks
- [Constraint Handling](./Constraints.md) - Working with route constraints

## References

1. Nearest Neighbor: Classic TSP greedy heuristic
2. 2-Opt: Croes, G. A. (1958). "A Method for Solving Traveling-Salesman Problems"
3. Genetic Algorithms: Goldberg, D. E. (1989). "Genetic Algorithms in Search, Optimization, and Machine Learning"
4. Simulated Annealing: Kirkpatrick, S., et al. (1983). "Optimization by Simulated Annealing"
