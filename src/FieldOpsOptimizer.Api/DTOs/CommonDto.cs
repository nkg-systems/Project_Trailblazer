using System.ComponentModel.DataAnnotations;

namespace FieldOpsOptimizer.Api.DTOs;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
    
    public static PagedResult<T> Create(List<T> items, int totalCount, int page, int pageSize)
    {
        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}

public class PaginationOptions
{
    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
    public int Page { get; set; } = 1;
    
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
    public int PageSize { get; set; } = 10;
    
    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
}

public class TechnicianFilter
{
    public string? EmployeeId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Status { get; set; }
    public List<string>? Skills { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? RadiusInKm { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableTo { get; set; }
}

public class ServiceJobFilter
{
    public string? JobNumber { get; set; }
    public string? Title { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? JobType { get; set; }
    public Guid? TechnicianId { get; set; }
    public DateTime? ScheduledFrom { get; set; }
    public DateTime? ScheduledTo { get; set; }
    public List<string>? RequiredSkills { get; set; }
    public string? CustomerName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? RadiusInKm { get; set; }
}

public class RouteFilter
{
    public string? RouteName { get; set; }
    public Guid? TechnicianId { get; set; }
    public DateTime? RouteDateFrom { get; set; }
    public DateTime? RouteDateTo { get; set; }
    public string? Status { get; set; }
    public double? MinDistance { get; set; }
    public double? MaxDistance { get; set; }
}

public class SortOptions
{
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; } = "asc"; // "asc" or "desc"
    
    public bool IsAscending => string.Equals(SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
}

public class ApiResponse<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public static ApiResponse<T> SuccessResult(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Data = data,
            Success = true,
            Message = message
        };
    }
    
    public static ApiResponse<T> ErrorResult(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}

public class ApiResponse
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public static ApiResponse SuccessResult(string? message = null)
    {
        return new ApiResponse
        {
            Success = true,
            Message = message
        };
    }
    
    public static ApiResponse ErrorResult(string message, List<string>? errors = null)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}

public class BulkOperationResult<T>
{
    public List<T> SuccessItems { get; set; } = new();
    public List<BulkOperationError> Errors { get; set; } = new();
    public int TotalProcessed => SuccessItems.Count + Errors.Count;
    public int SuccessCount => SuccessItems.Count;
    public int ErrorCount => Errors.Count;
    public bool HasErrors => Errors.Count > 0;
}

public class BulkOperationError
{
    public int Index { get; set; }
    public string Item { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
