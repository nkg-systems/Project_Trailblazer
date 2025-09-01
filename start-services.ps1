# Field Operations Optimizer - Service Startup Script
# This script helps you start different combinations of services

param(
    [Parameter(HelpMessage="Start core services only (postgres, redis, rabbitmq)")]
    [switch]$Core,
    
    [Parameter(HelpMessage="Start monitoring services (prometheus, grafana)")]
    [switch]$Monitoring,
    
    [Parameter(HelpMessage="Start logging services (seq)")]
    [switch]$Logging,
    
    [Parameter(HelpMessage="Start tracing services (jaeger)")]
    [switch]$Tracing,
    
    [Parameter(HelpMessage="Start routing services (osrm)")]
    [switch]$Routing,
    
    [Parameter(HelpMessage="Start all services")]
    [switch]$All,
    
    [Parameter(HelpMessage="Stop all services")]
    [switch]$Stop
)

Write-Host "Field Operations Optimizer - Docker Service Manager" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green

if ($Stop) {
    Write-Host "[STOP] Stopping all services..." -ForegroundColor Yellow
    docker compose -f docker-compose.core.yml down
    docker compose down
    Write-Host "[SUCCESS] All services stopped" -ForegroundColor Green
    exit
}

if ($Core -or $All) {
    Write-Host "[CORE] Starting core services (PostgreSQL, Redis, RabbitMQ)..." -ForegroundColor Cyan
    docker compose -f docker-compose.core.yml up -d postgres redis rabbitmq
    
    Write-Host "[WAIT] Waiting for services to be ready..." -ForegroundColor Yellow
    Start-Sleep -Seconds 10
    
    Write-Host "[SUCCESS] Core services started!" -ForegroundColor Green
    Write-Host "   PostgreSQL: localhost:5432 (user: $env:POSTGRES_USER)" -ForegroundColor Gray
    Write-Host "   Redis: localhost:6379 (authentication enabled)" -ForegroundColor Gray
    Write-Host "   RabbitMQ Management: http://localhost:15672 (user: $env:RABBITMQ_DEFAULT_USER)" -ForegroundColor Gray
}

if ($Monitoring -or $All) {
    Write-Host "[MONITORING] Starting monitoring services (Prometheus, Grafana)..." -ForegroundColor Cyan
    docker compose -f docker-compose.core.yml --profile monitoring up -d
    
    Write-Host "[SUCCESS] Monitoring services started!" -ForegroundColor Green
    Write-Host "   Prometheus: http://localhost:9090" -ForegroundColor Gray
    Write-Host "   Grafana: http://localhost:3000 (user: $env:GRAFANA_ADMIN_USER)" -ForegroundColor Gray
}

if ($Logging -or $All) {
    Write-Host "[LOGGING] Starting logging services (Seq)..." -ForegroundColor Cyan
    docker compose -f docker-compose.core.yml --profile logging up -d
    
    Write-Host "[SUCCESS] Logging services started!" -ForegroundColor Green
    Write-Host "   Seq: http://localhost:5341 (admin user configured)" -ForegroundColor Gray
}

if ($Tracing -or $All) {
    Write-Host "[TRACING] Starting tracing services (Jaeger)..." -ForegroundColor Cyan
    docker compose -f docker-compose.core.yml --profile tracing up -d
    
    Write-Host "[SUCCESS] Tracing services started!" -ForegroundColor Green
    Write-Host "   Jaeger: http://localhost:16686" -ForegroundColor Gray
}

if ($Routing -or $All) {
    Write-Host "[ROUTING] Starting routing services (OSRM)..." -ForegroundColor Cyan
    Write-Host "[WARNING] Note: OSRM requires map data initialization which may take several minutes" -ForegroundColor Yellow
    docker compose -f docker-compose.core.yml --profile routing up -d
    
    Write-Host "[SUCCESS] Routing services started!" -ForegroundColor Green
    Write-Host "   OSRM: http://localhost:5000" -ForegroundColor Gray
}

if (-not ($Core -or $Monitoring -or $Logging -or $Tracing -or $Routing -or $All)) {
    Write-Host "[INFO] Usage examples:" -ForegroundColor Yellow
    Write-Host "  .\start-services.ps1 -Core                    # Start just the core services" -ForegroundColor Gray
    Write-Host "  .\start-services.ps1 -Core -Monitoring        # Start core + monitoring" -ForegroundColor Gray
    Write-Host "  .\start-services.ps1 -All                     # Start all services" -ForegroundColor Gray
    Write-Host "  .\start-services.ps1 -Stop                    # Stop all services" -ForegroundColor Gray
    Write-Host ""
    Write-Host "[SERVICES] Available service groups:" -ForegroundColor Cyan
    Write-Host "  Core:       PostgreSQL, Redis, RabbitMQ" -ForegroundColor Gray
    Write-Host "  Monitoring: Prometheus, Grafana" -ForegroundColor Gray
    Write-Host "  Logging:    Seq structured logging" -ForegroundColor Gray
    Write-Host "  Tracing:    Jaeger distributed tracing" -ForegroundColor Gray
    Write-Host "  Routing:    OSRM routing engine" -ForegroundColor Gray
}

Write-Host ""
Write-Host "[STATUS] Check service status with: docker compose ps" -ForegroundColor Cyan
