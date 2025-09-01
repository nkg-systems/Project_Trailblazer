# Field Operations Optimizer

A comprehensive .NET 9/C# field operations optimizer that ingests open routing/traffic data, free weather APIs, and webhook/CSV job feeds to auto-schedule technicians, predict ETAs, cluster routes, and reduce idle miles for service SMBs.

## 🚀 Features

### Core Functionality
- **Intelligent Route Optimization**: Uses OpenStreetMap + OSRM/Valhalla for route calculation and optimization
- **Real-time Weather Integration**: Open-Meteo/NOAA weather data for scheduling optimization
- **Multi-source Job Ingestion**: Webhook and CSV job feed support
- **ETA Prediction**: ML.NET-powered demand forecasting and ETA calculation
- **Technician Auto-scheduling**: Skills-based matching and availability optimization
- **Multi-tenant Support**: Built-in tenant isolation for service SMBs

### Technical Stack
- **Architecture**: Clean Architecture + CQRS/MediatR pattern
- **Backend**: ASP.NET Core minimal APIs + gRPC services
- **Frontend**: Blazor WebAssembly with MapLibre GL mapping
- **Database**: PostgreSQL with Entity Framework Core
- **Caching**: Redis for performance optimization
- **Background Processing**: Hangfire OSS + MassTransit + RabbitMQ
- **Authentication**: OpenIddict + ASP.NET Identity
- **Observability**: OpenTelemetry → Prometheus + Grafana
- **Search**: PostgreSQL Full-Text Search
- **Feature Flags**: Unleash OSS
- **Real-time Updates**: SignalR
- **Resilience**: Polly for robust external service integration

### Infrastructure & DevOps
- **Containerization**: Docker + Docker Compose
- **Orchestration**: k3d/k3s support
- **CI/CD**: GitHub Actions
- **IaC**: Infrastructure as Code approach
- **Monitoring**: Full observability stack with metrics, tracing, and logging

## 🏗 Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                       │
├─────────────────────┬───────────────────────────────────────┤
│   Blazor WASM UI    │        ASP.NET Core API              │
│   + MapLibre GL     │     (Minimal APIs + gRPC)            │
│   + SignalR Client  │     + SignalR Hub                     │
└─────────────────────┼───────────────────────────────────────┘
                      │
┌─────────────────────┼───────────────────────────────────────┐
│                 Application Layer                           │
├─────────────────────┼───────────────────────────────────────┤
│              CQRS + MediatR                                 │
│    Commands, Queries, Handlers, Validators                  │
└─────────────────────┼───────────────────────────────────────┘
                      │
┌─────────────────────┼───────────────────────────────────────┐
│               Infrastructure Layer                          │
├─────────────────────┼───────────────────────────────────────┤
│  EF Core + PostgreSQL  │  External Services Integration    │
│  Redis Caching         │  • OpenStreetMap/OSRM            │
│  Hangfire/MassTransit  │  • Open-Meteo Weather             │
│  OpenTelemetry         │  • Webhook/CSV Ingestion          │
└─────────────────────┴───────────────────────────────────────┘
                      │
┌─────────────────────┼───────────────────────────────────────┐
│                 Domain Layer                                │
├─────────────────────┼───────────────────────────────────────┤
│  Entities, Value Objects, Domain Services                   │
│  Technician, ServiceJob, Route, etc.                       │
└─────────────────────────────────────────────────────────────┘
```

## 🚦 Quick Start

### Prerequisites
- [.NET 7+ SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Git](https://git-scm.com/)

### One-Command Local Deploy

#### Option 1: Using PowerShell Script (Recommended)
```powershell
# Start core services (PostgreSQL, Redis, RabbitMQ)
.\start-services.ps1 -Core

# Or start all services
.\start-services.ps1 -All

# Stop all services
.\start-services.ps1 -Stop
```

#### Option 2: Using Docker Compose Directly
```bash
# Start core services only
docker compose -f docker-compose.core.yml up -d postgres redis rabbitmq

# Start with monitoring
docker compose -f docker-compose.core.yml --profile monitoring up -d

# Or start everything (may take longer due to OSRM initialization)
docker compose up -d
```

#### Database Setup
```bash
# Run database migrations (after PostgreSQL is running)
dotnet ef database update --project src/FieldOpsOptimizer.Infrastructure --startup-project src/FieldOpsOptimizer.Api

# Start the API
dotnet run --project src/FieldOpsOptimizer.Api

# Start the Blazor WASM app  
dotnet run --project src/FieldOpsOptimizer.Web
```

### Access Points

After starting the services:

- **Main Application**: http://localhost:5002
- **API Documentation**: http://localhost:5001/swagger
- **Grafana Dashboard**: http://localhost:3000 (see .env for credentials)
- **Prometheus**: http://localhost:9090
- **RabbitMQ Management**: http://localhost:15672 (see .env for credentials)
- **Seq Logging**: http://localhost:5341 (see .env for credentials)
- **Jaeger Tracing**: http://localhost:16686
- **Unleash Feature Flags**: http://localhost:4242
- **MinIO Console**: http://localhost:9001 (see .env for credentials)

## 📊 Project Status

### Completed ✅
- [x] Clean Architecture foundation
- [x] Domain modeling (Technician, ServiceJob, Route entities)
- [x] CQRS/MediatR infrastructure
- [x] PostgreSQL + EF Core data layer
- [x] Repository pattern implementation
- [x] Docker Compose infrastructure setup
- [x] Basic CRUD operations for technicians

### In Progress 🚧
- [ ] External service integrations (OSRM, Open-Meteo)
- [ ] Route optimization engine
- [ ] ASP.NET Core API implementation
- [ ] Blazor WASM frontend
- [ ] Authentication & authorization

### Planned 📋
- [ ] Background job processing
- [ ] ML.NET forecasting
- [ ] SignalR real-time updates
- [ ] Observability stack integration
- [ ] Load testing & benchmarks

## 🧪 Testing

### Unit Tests
```bash
dotnet test tests/FieldOpsOptimizer.Domain.Tests
dotnet test tests/FieldOpsOptimizer.Application.Tests
```

## 📁 Project Structure

```
src/
├── FieldOpsOptimizer.Domain/           # Domain entities and business logic
├── FieldOpsOptimizer.Application/      # Application services and CQRS
├── FieldOpsOptimizer.Infrastructure/   # Data access and external services
├── FieldOpsOptimizer.Api/             # REST/gRPC API endpoints
├── FieldOpsOptimizer.Web/             # Blazor WASM frontend
└── FieldOpsOptimizer.Simulator/       # Demo data generation

tests/
├── FieldOpsOptimizer.Domain.Tests/    # Domain unit tests
└── FieldOpsOptimizer.Application.Tests/ # Application unit tests

infrastructure/
├── prometheus/                        # Prometheus configuration
├── grafana/                          # Grafana dashboards
├── osrm/                            # OSRM routing data
└── postgres/                        # Database initialization
```
Cross-Platform Process Capture &amp; Action Studio
