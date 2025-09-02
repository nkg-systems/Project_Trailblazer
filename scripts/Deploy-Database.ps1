# Deploy-Database.ps1
# Database deployment script for FieldOpsOptimizer

param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "Development",
    
    [Parameter(Mandatory=$false)]
    [string]$ConnectionString = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$SeedData = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force = $false
)

# Configuration
$ProjectRoot = Split-Path $PSScriptRoot
$InfrastructureProject = Join-Path $ProjectRoot "src\FieldOpsOptimizer.Infrastructure"
$ApiProject = Join-Path $ProjectRoot "src\FieldOpsOptimizer.Api"

Write-Host "=== FieldOpsOptimizer Database Deployment ===" -ForegroundColor Green
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Yellow

# Verify dotnet EF tools are installed
Write-Host "Checking Entity Framework tools..." -ForegroundColor Blue
try {
    $efVersion = dotnet ef --version 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Installing Entity Framework tools..." -ForegroundColor Yellow
        dotnet tool install --global dotnet-ef
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install Entity Framework tools"
        }
    } else {
        Write-Host "Entity Framework tools are available: $efVersion" -ForegroundColor Green
    }
} catch {
    Write-Error "Failed to verify or install Entity Framework tools: $_"
    exit 1
}

# Set environment variable for EF Core
$env:ASPNETCORE_ENVIRONMENT = $Environment

# If connection string is provided, set it as environment variable
if ($ConnectionString) {
    $env:ConnectionStrings__DefaultConnection = $ConnectionString
    Write-Host "Using provided connection string" -ForegroundColor Yellow
}

# Build the projects
Write-Host "Building projects..." -ForegroundColor Blue
dotnet build $InfrastructureProject --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build Infrastructure project"
    exit 1
}

dotnet build $ApiProject --configuration Release --no-restore  
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build API project"
    exit 1
}

Write-Host "Projects built successfully" -ForegroundColor Green

# Check database connection
Write-Host "Checking database connection..." -ForegroundColor Blue
try {
    $connectionResult = dotnet ef database drop --dry-run --project $InfrastructureProject --startup-project $ApiProject 2>&1
    if ($connectionResult -like "*Cannot connect to database*") {
        Write-Warning "Cannot connect to database. Please ensure PostgreSQL is running and connection string is correct."
        if (-not $Force) {
            $continue = Read-Host "Continue anyway? (y/N)"
            if ($continue -ne "y" -and $continue -ne "Y") {
                exit 1
            }
        }
    } else {
        Write-Host "Database connection verified" -ForegroundColor Green
    }
} catch {
    Write-Warning "Could not verify database connection: $_"
}

# Apply migrations
Write-Host "Applying database migrations..." -ForegroundColor Blue
$migrateArgs = @(
    "ef", "database", "update",
    "--project", $InfrastructureProject,
    "--startup-project", $ApiProject
)

if ($VerbosePreference -eq 'Continue') {
    $migrateArgs += "--verbose"
}

& dotnet $migrateArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to apply database migrations"
    exit 1
}

Write-Host "Database migrations applied successfully" -ForegroundColor Green

# Seed data if requested
if ($SeedData) {
    Write-Host "Seeding database with initial data..." -ForegroundColor Blue
    
    # Create a temporary seeding program
    $seedScript = @"
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using FieldOpsOptimizer.Infrastructure.Data;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<DatabaseSeeder>();
    })
    .Build();

using var scope = host.Services.CreateScope();
var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Starting database seeding...");
    await seeder.SeedAsync();
    logger.LogInformation("Database seeding completed successfully");
    Environment.Exit(0);
}
catch (Exception ex)
{
    logger.LogError(ex, "Database seeding failed");
    Environment.Exit(1);
}
"@

    $tempDir = Join-Path $env:TEMP "FieldOpsSeeder"
    $tempProject = Join-Path $tempDir "Seeder.csproj"
    $tempProgram = Join-Path $tempDir "Program.cs"
    
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    # Create temporary project
    $projectContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net7.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$InfrastructureProject\FieldOpsOptimizer.Infrastructure.csproj" />
    <ProjectReference Include="$ApiProject\FieldOpsOptimizer.Api.csproj" />
  </ItemGroup>
</Project>
"@

    $projectContent | Out-File -FilePath $tempProject -Encoding UTF8
    $seedScript | Out-File -FilePath $tempProgram -Encoding UTF8
    
    # Run seeding
    Push-Location $tempDir
    try {
        dotnet run --configuration Release
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Database seeding completed successfully" -ForegroundColor Green
        } else {
            Write-Warning "Database seeding failed or had warnings"
        }
    } finally {
        Pop-Location
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Display migration status
Write-Host "Checking migration status..." -ForegroundColor Blue
dotnet ef migrations list --project $InfrastructureProject --startup-project $ApiProject

Write-Host "=== Database Deployment Complete ===" -ForegroundColor Green
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Timestamp: $(Get-Date)" -ForegroundColor Yellow

# Clean up environment variables
if ($ConnectionString) {
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
}
Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
