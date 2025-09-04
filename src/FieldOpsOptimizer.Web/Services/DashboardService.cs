using FieldOpsOptimizer.Web.Models;
using System.Net.Http.Json;

namespace FieldOpsOptimizer.Web.Services;

public class DashboardService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(HttpClient httpClient, ILogger<DashboardService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<DashboardStats>("/api/dashboard/stats");
            return response ?? CreateMockDashboardStats();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch dashboard stats from API, using mock data");
            return CreateMockDashboardStats();
        }
    }

    public async Task<List<RecentActivity>> GetRecentActivitiesAsync(int count = 10)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<RecentActivity>>($"/api/dashboard/activities?count={count}");
            return response ?? CreateMockRecentActivities();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch recent activities from API, using mock data");
            return CreateMockRecentActivities();
        }
    }

    public async Task<WeatherInfo> GetWeatherInfoAsync(string location = "Default")
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<WeatherInfo>($"/api/dashboard/weather?location={location}");
            return response ?? CreateMockWeatherInfo();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch weather info from API, using mock data");
            return CreateMockWeatherInfo();
        }
    }

    // Mock data methods for development/demo purposes
    private static DashboardStats CreateMockDashboardStats()
    {
        var random = new Random();
        return new DashboardStats
        {
            TotalTechnicians = 12,
            TechniciansOnDuty = 8,
            ActiveRoutes = 6,
            PendingJobs = 23,
            CompletedJobsToday = 45,
            InProgressJobs = 12,
            CompletionRate = 87.5m,
            AverageJobDuration = TimeSpan.FromHours(2.3),
            TotalDistanceTraveledToday = 342.7,
            FuelSavingsToday = 156.80m,
            OverdueJobs = 3,
            LastUpdated = DateTime.Now.AddMinutes(-random.Next(1, 10))
        };
    }

    private static List<RecentActivity> CreateMockRecentActivities()
    {
        return new List<RecentActivity>
        {
            new()
            {
                Id = "1",
                Type = "Job",
                Description = "Job JOB20241201-045 completed by John Smith",
                Timestamp = DateTime.Now.AddMinutes(-5),
                Status = "Completed",
                Icon = "oi-check",
                Color = "success"
            },
            new()
            {
                Id = "2",
                Type = "Route",
                Description = "Route 'Downtown Morning' started by Sarah Johnson",
                Timestamp = DateTime.Now.AddMinutes(-12),
                Status = "In Progress",
                Icon = "oi-map",
                Color = "info"
            },
            new()
            {
                Id = "3",
                Type = "Technician",
                Description = "Mike Wilson checked in at customer location",
                Timestamp = DateTime.Now.AddMinutes(-18),
                Status = "Active",
                Icon = "oi-location-pin",
                Color = "primary"
            },
            new()
            {
                Id = "4",
                Type = "Job",
                Description = "New high priority job JOB20241201-046 created",
                Timestamp = DateTime.Now.AddMinutes(-25),
                Status = "Scheduled",
                Icon = "oi-plus",
                Color = "warning"
            },
            new()
            {
                Id = "5",
                Type = "Route",
                Description = "Route optimization completed - saved 23 minutes",
                Timestamp = DateTime.Now.AddMinutes(-30),
                Status = "Optimized",
                Icon = "oi-graph",
                Color = "success"
            }
        };
    }

    private static WeatherInfo CreateMockWeatherInfo()
    {
        var conditions = new[] { "Sunny", "Partly Cloudy", "Cloudy", "Light Rain", "Clear" };
        var icons = new[] { "☀️", "⛅", "☁️", "🌦️", "🌤️" };
        var random = new Random();
        var conditionIndex = random.Next(conditions.Length);

        return new WeatherInfo
        {
            Location = "Service Area",
            Temperature = random.Next(15, 30),
            Condition = conditions[conditionIndex],
            Icon = icons[conditionIndex],
            Humidity = random.Next(30, 80),
            WindSpeed = random.Next(5, 25),
            WindDirection = "NW",
            LastUpdated = DateTime.Now.AddMinutes(-random.Next(5, 30))
        };
    }
}
