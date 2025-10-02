using Microsoft.EntityFrameworkCore;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Infrastructure.Data.Repositories;
using FieldOpsOptimizer.Infrastructure.Optimization;
using FieldOpsOptimizer.Application.Common.Models;
using FieldOpsOptimizer.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FieldOpsOptimizer.Api.Middleware;
using FieldOpsOptimizer.Api.Infrastructure.Middleware;
using FieldOpsOptimizer.Api.Common;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using FieldOpsOptimizer.Api.Mapping;
using FieldOpsOptimizer.Api;
using AspNetCoreRateLimit;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure logging and monitoring first
builder.ConfigureLoggingAndMonitoring();

// Add services to the container.

// Configure Database
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsDevelopment())
{
    if (string.IsNullOrEmpty(connectionString))
    {
        // Fallback to in-memory database for local development
        Console.WriteLine("WARNING: No database connection string found. Using in-memory database.");
        Console.WriteLine("Set DATABASE_CONNECTION_STRING environment variable for persistent database.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));
    }
    else
    {
        // Use PostgreSQL for development if connection string is provided
        Console.WriteLine("Using database connection from environment variable or configuration.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("FieldOpsOptimizer.Infrastructure");
                npgsqlOptions.CommandTimeout(30);
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            })
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors());
    }
}
else
{
    // Production database configuration - environment variable is required
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database connection string not found. Please set the DATABASE_CONNECTION_STRING environment variable.");
    }
    
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("FieldOpsOptimizer.Infrastructure");
            npgsqlOptions.CommandTimeout(60); // Longer timeout for production
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        }));
}

// Configure AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add memory cache for weather service and rate limiting
builder.Services.AddMemoryCache();

// Add rate limiting
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// Register repositories and services
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register core data features services
builder.Services.AddScoped<IJobNoteService, JobNoteService>();
builder.Services.AddScoped<IJobStatusHistoryService, JobStatusHistoryService>();

// Register tenant service
builder.Services.AddScoped<ITenantService, TenantService>();

// Register optimization services
builder.Services.AddScoped<IRouteOptimizationService, RouteOptimizationService>();
builder.Services.AddScoped<NearestNeighborOptimizer>();
builder.Services.AddScoped<TwoOptOptimizer>();
builder.Services.AddScoped<GeneticOptimizer>();

// Register weather service
builder.Services.Configure<WeatherSettings>(builder.Configuration.GetSection(WeatherSettings.ConfigurationSection));
builder.Services.AddHttpClient<IWeatherService, OpenWeatherMapService>();
builder.Services.AddScoped<IWeatherService, OpenWeatherMapService>();

// Register authentication services
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRepository<FieldOpsOptimizer.Domain.Entities.User>, Repository<FieldOpsOptimizer.Domain.Entities.User>>();

// Configure JWT authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();

// Get JWT secret from environment variables (secure approach)
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? jwtSettings.Secret;
if (string.IsNullOrEmpty(jwtSecret))
{
    if (builder.Environment.IsDevelopment())
    {
        // For development, allow a fallback to a development-only secret
        jwtSecret = "DevOnlySecret_FieldOpsOptimizer_MinimumLength32CharactersForSecurity!";
        Console.WriteLine("WARNING: Using development JWT secret. Set JWT_SECRET environment variable for production.");
    }
    else
    {
        throw new InvalidOperationException("JWT Secret is not configured. Please set the JWT_SECRET environment variable.");
    }
}

// Override the secret with the secure one
jwtSettings.Secret = jwtSecret;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment(); // Allow HTTP in development, require HTTPS in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.Zero // Remove delay of token when expire
    };
});

builder.Services.AddAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Allow all origins in development for easier testing
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Restrict origins in production - configure these based on your frontend domains
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
                ?? new[] { "https://yourdomain.com", "https://app.yourdomain.com" };
            
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials() // Allow cookies/auth headers
                  .WithExposedHeaders("X-Pagination"); // Expose pagination headers if needed
        }
    });
});

// Configure global exception handling
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<ProblemDetailsFactory, CustomProblemDetailsFactory>();

// Configure API behavior for automatic model validation
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = false;
    options.SuppressMapClientErrors = false;
});

// Add Anti-Forgery token support
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false; // Keep X-Frame-Options header
    options.Cookie.Name = "__RequestVerificationToken";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Health checks are configured in ConfigureLoggingAndMonitoring()

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Field Operations Optimizer API", Version = "v1" });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// Add background services
builder.Services.AddHostedService<MetricsUpdateService>();

var app = builder.Build();

// Configure the monitoring pipeline
app.ConfigureMonitoringPipeline();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add security headers middleware early in the pipeline
app.UseSecurityHeaders();

// Add rate limiting
app.UseIpRateLimiting();

// Add CORS
app.UseCors("DefaultPolicy");

// Authentication and authorization are configured in ConfigureMonitoringPipeline()
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Use enhanced startup and shutdown with proper logging
try
{
    await app.StartWithLoggingAsync();
    await app.WaitForShutdownAsync();
}
finally
{
    await app.StopWithLoggingAsync();
}

// Make Program class accessible for integration testing
public partial class Program { }
