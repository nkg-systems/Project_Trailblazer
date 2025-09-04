namespace FieldOpsOptimizer.Web.Models;

public class DashboardStats
{
    public int TotalTechnicians { get; set; }
    public int TechniciansOnDuty { get; set; }
    public int ActiveRoutes { get; set; }
    public int PendingJobs { get; set; }
    public int CompletedJobsToday { get; set; }
    public int InProgressJobs { get; set; }
    public decimal CompletionRate { get; set; }
    public TimeSpan AverageJobDuration { get; set; }
    public double TotalDistanceTraveledToday { get; set; }
    public decimal FuelSavingsToday { get; set; }
    public int OverdueJobs { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

public class RecentActivity
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Job, Route, Technician
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}

public class WeatherInfo
{
    public string Location { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Humidity { get; set; }
    public double WindSpeed { get; set; }
    public string WindDirection { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
