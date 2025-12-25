# Field Operations Optimizer

A comprehensive .NET 7/C# field operations optimizer that ingests open routing/traffic data, free weather APIs, and webhook/CSV job feeds to auto-schedule technicians, predict ETAs, cluster routes, and reduce idle miles for service SMBs.

[![.NET](https://img.shields.io/badge/.NET-7.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/7.0)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-blue.svg)](https://postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-Compose-blue.svg)](https://docker.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

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

## 🔄 CI/CD Pipeline

### GitHub Actions Workflows

The project includes comprehensive CI/CD automation:

#### Continuous Integration (`ci.yml`)
Runs on every push and pull request to `main` and `develop` branches:
- **Build & Test**: Compiles solution and runs all 249 tests
- **Code Coverage**: Generates coverage reports with Coverlet
- **Code Quality**: Validates code formatting and checks for warnings
- **Security Scan**: Scans dependencies for known vulnerabilities
- **PR Comments**: Automatically adds test results and coverage to PRs

```bash
# Triggered automatically on:
- Push to main/develop
- Pull requests to main/develop

# Manual trigger:
gh workflow run ci.yml
```

#### Docker Build (`docker-build.yml`)
Builds and publishes Docker images to GitHub Container Registry:
- **Multi-stage Builds**: Optimized Docker images for API and Web
- **Multi-platform**: Supports linux/amd64 and linux/arm64
- **Security Scanning**: Trivy scans for container vulnerabilities
- **Artifact Attestation**: Generates SLSA provenance

```bash
# Triggered on:
- Push to main
- Version tags (v*.*.*)  
- Manual workflow dispatch

# Images published to:
ghcr.io/<username>/field-ops-optimizer-api:latest
ghcr.io/<username>/field-ops-optimizer-web:latest
```

#### Continuous Deployment (`cd-deploy.yml`)
Manual deployment workflow with environment-specific configurations:
- **Environments**: Development, Staging, Production
- **Version Control**: Deploy specific versions or latest
- **Approval Gates**: Manual approval required for production
- **Smoke Tests**: Automated health checks after deployment
- **Rollback**: Automatic rollback on deployment failures

```bash
# Manual deployment:
gh workflow run cd-deploy.yml -f environment=staging -f version=v1.2.3
```

### Setting Up CI/CD

1. **GitHub Container Registry**: Ensure GitHub Packages is enabled for your repository
2. **Environments**: Configure environments in GitHub repository settings:
   - `development`: Auto-deploy on successful build
   - `staging`: Requires one reviewer
   - `production`: Requires two reviewers + branch protection
3. **Secrets**: No additional secrets required (uses `GITHUB_TOKEN`)

### Local Docker Build

```bash
# Build API image
docker build -f src/FieldOpsOptimizer.Api/Dockerfile -t field-ops-api:local .

# Build Web image
docker build -f src/FieldOpsOptimizer.Web/Dockerfile -t field-ops-web:local .

# Run locally
docker run -p 8080:8080 field-ops-api:local
docker run -p 80:80 field-ops-web:local
```

## 📊 Project Status

**Last Updated**: December 24, 2024  
**Phase**: CI/CD & DevOps (Phase 5 Complete - Automated Pipeline! 🚀)

### Core Platform - Completed ✅
- [x] Clean Architecture foundation with Domain-Driven Design
- [x] Domain modeling (Technician, ServiceJob, Route entities)
- [x] CQRS/MediatR infrastructure with command/query separation
- [x] PostgreSQL + Entity Framework Core data layer
- [x] Repository pattern implementation with UoW
- [x] Docker Compose infrastructure setup
- [x] ASP.NET Core Web API with RESTful endpoints
- [x] Blazor WebAssembly frontend with responsive UI
- [x] Health checks for database and external services
- [x] Comprehensive logging and error handling
- [x] API documentation with Swagger/OpenAPI

### Data & Operations - Completed ✅
- [x] Technician management (CRUD operations)
- [x] Service job management and tracking
- [x] Route optimization engine foundation
- [x] Weather service integration (Open-Meteo)
- [x] External routing service integration (OSRM)
- [x] Database migrations and seeding
- [x] Data validation and business rules

### Infrastructure & DevOps - Completed ✅
- [x] Multi-container Docker setup
- [x] PostgreSQL database container
- [x] Redis caching layer
- [x] RabbitMQ message broker
- [x] Monitoring stack (Prometheus, Grafana)
- [x] Distributed tracing (Jaeger)
- [x] Centralized logging (Seq)
- [x] Feature flags (Unleash)
- [x] Object storage (MinIO)
- [x] PowerShell deployment scripts

### Code Quality & Security - Completed ✅ (All Phases)
- [x] **Phase 1**: Fixed all API controller compiler warnings
- [x] **Phase 1**: Resolved enum mismatches between DTO and Domain layers
- [x] **Phase 1**: Fixed async method warnings in API controllers
- [x] **Phase 2**: Fixed AuthService security issues (proper logging, error handling)
- [x] **Phase 2**: Fixed WeatherService and TenantService async warnings
- [x] **Phase 3**: Fixed all optimization algorithm warnings (async, nullability)
- [x] **Phase 3**: Fixed OSRMRoutingService null reference warning
- [x] **Phase 4**: Enabled TreatWarningsAsErrors across all projects
- [x] **Phase 4**: Hardened tenant security (JWT-only, no header/query spoofing)
- [x] **Phase 4**: Fixed WeatherData nullability issues
- [x] **100% Warning Elimination**: 16 → 0 warnings
- [x] **Phase 5**: Comprehensive test coverage (249 tests - Domain, Application, Infrastructure)
- [x] **Phase 5**: CI/CD pipeline with GitHub Actions
- [x] All tests passing (249 tests)
- [x] Build succeeds with 0 errors, 0 warnings, TreatWarningsAsErrors enforced

### CI/CD & DevOps - Completed ✅
- [x] GitHub Actions CI pipeline (build, test, code quality, security scan)
- [x] Automated test execution (249 tests)
- [x] Code coverage reporting with Coverlet
- [x] Docker image build and push to GitHub Container Registry
- [x] Multi-stage Docker builds for API and Web
- [x] Security scanning with Trivy
- [x] CD pipeline with environment-specific deployments (dev, staging, production)
- [x] Manual approval gates for production deployments
- [x] Automated rollback on deployment failures

### Test Coverage - Completed ✅
- [x] **Domain Tests**: 142 tests (entities, value objects, domain services)
- [x] **Application Tests**: 28 tests (CQRS handlers, validators)
- [x] **Infrastructure Tests**: 79 tests (optimization algorithms, external services, auth)
- [x] **Total Coverage**: 249 tests with 100% pass rate

### In Progress 🚧
- [ ] Advanced route optimization algorithms (genetic algorithms, simulated annealing)
- [ ] ML.NET integration for demand forecasting
- [ ] Real-time updates with SignalR
- [ ] Performance optimization and caching strategies
- [ ] Kubernetes deployment manifests

### Planned 📋
- [ ] Background job processing with Hangfire
- [ ] Mobile app (Xamarin/MAUI)
- [ ] Advanced analytics dashboard
- [ ] Integration with external CRM systems
- [ ] Load testing & performance benchmarks

## 🧪 Testing

### Unit Tests
```bash
dotnet test tests/FieldOpsOptimizer.Domain.Tests
dotnet test tests/FieldOpsOptimizer.Application.Tests
```

### Integration Tests
```bash
dotnet test tests/FieldOpsOptimizer.Api.Tests
```

### Run All Tests
```bash
dotnet test
```

## 🏛️ Architecture Overview

Built following Clean Architecture principles with clear separation of concerns:

- **Domain Layer**: Core business entities, value objects, and domain services
- **Application Layer**: Use cases, CQRS commands/queries, and application services
- **Infrastructure Layer**: Data persistence, external service integrations, and cross-cutting concerns
- **Presentation Layer**: Web API controllers and Blazor WebAssembly UI components

## 📁 Project Structure

```
src/
├── FieldOpsOptimizer.Domain/           # 🏢 Domain entities and business logic
│   ├── Entities/                       # Core business entities (Technician, Job, Route)
│   ├── ValueObjects/                   # Domain value objects
│   ├── Services/                       # Domain services
│   └── Exceptions/                     # Domain-specific exceptions
│
├── FieldOpsOptimizer.Application/      # 🔧 Application services and CQRS
│   ├── Features/                       # Feature-based organization
│   │   └── Technicians/               # Commands, queries, and handlers
│   └── Common/                        # Shared interfaces and models
│
├── FieldOpsOptimizer.Infrastructure/   # 🔌 Data access and external services
│   ├── Data/                          # EF Core contexts, configurations, migrations
│   ├── ExternalServices/              # Weather, routing service clients
│   ├── Optimization/                  # Route optimization algorithms
│   └── Services/                      # Infrastructure service implementations
│
├── FieldOpsOptimizer.Api/             # 🌐 REST API endpoints and infrastructure
│   ├── Controllers/                   # API controllers
│   ├── Middleware/                    # Custom middleware
│   ├── Infrastructure/                # Health checks, metrics, tracing
│   └── DTOs/                          # Data transfer objects
│
├── FieldOpsOptimizer.Web/             # 💻 Blazor WebAssembly frontend
│   ├── Pages/                         # Razor pages and components
│   ├── Components/                    # Reusable UI components
│   ├── Services/                      # Client-side services
│   └── Shared/                        # Shared layouts and components
│
└── FieldOpsOptimizer.Simulator/       # 🎲 Demo data generation and testing

tests/
├── FieldOpsOptimizer.Domain.Tests/      # 🧪 Domain unit tests
├── FieldOpsOptimizer.Application.Tests/ # 🧪 Application unit tests
└── FieldOpsOptimizer.Api.Tests/        # 🧪 API integration tests

infrastructure/                        # 🐳 Container and infrastructure configs
├── prometheus/                        # Metrics collection configuration
├── grafana/                          # Monitoring dashboards
├── osrm/                            # Routing engine data and configs
├── postgres/                        # Database initialization scripts
└── scripts/                         # Deployment and utility scripts

docs/                                  # 📚 Additional documentation
scripts/                               # 📜 PowerShell deployment scripts
├── Deploy-Database.ps1
├── start-services.ps1
└── security-check.ps1
```

## 💻 API Documentation

### API Endpoints

The API follows RESTful conventions and includes comprehensive OpenAPI/Swagger documentation.

**Base URL**: `http://localhost:5001/api`

#### Technician Management
- `GET /api/technicians` - List all technicians
- `GET /api/technicians/{id}` - Get technician by ID
- `POST /api/technicians` - Create new technician
- `PUT /api/technicians/{id}` - Update technician
- `DELETE /api/technicians/{id}` - Delete technician

#### Service Jobs
- `GET /api/servicejobs` - List service jobs
- `POST /api/servicejobs` - Create new service job
- `PUT /api/servicejobs/{id}/assign` - Assign job to technician

#### Route Optimization
- `POST /api/routes/optimize` - Optimize routes for technicians
- `GET /api/routes/{id}` - Get route details

#### Health Checks
- `GET /health` - Application health status
- `GET /health/ready` - Readiness probe
- `GET /health/live` - Liveness probe

### Authentication

Currently in development. The API will support:
- JWT Bearer tokens
- Role-based access control (RBAC)
- Multi-tenant isolation

## 🔧 Development

### Prerequisites

- [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [PostgreSQL](https://www.postgresql.org/download/) (for local development without Docker)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd Project_Trailblazer
   ```

2. **Start infrastructure services**
   ```powershell
   # Start core services (PostgreSQL, Redis, RabbitMQ)
   .\start-services.ps1 -Core
   ```

3. **Setup database**
   ```bash
   # Apply migrations
   dotnet ef database update --project src/FieldOpsOptimizer.Infrastructure --startup-project src/FieldOpsOptimizer.Api
   ```

4. **Run the application**
   ```bash
   # Terminal 1 - API
   dotnet run --project src/FieldOpsOptimizer.Api
   
   # Terminal 2 - Web UI
   dotnet run --project src/FieldOpsOptimizer.Web
   ```

### Environment Variables

Create a `.env` file in the root directory:

```env
# Database
POSTGRES_DB=fieldops_db
POSTGRES_USER=fieldops_user
POSTGRES_PASSWORD=your_secure_password

# Redis
REDIS_PASSWORD=your_redis_password

# RabbitMQ
RABBITMQ_DEFAULT_USER=admin
RABBITMQ_DEFAULT_PASS=your_rabbitmq_password

# External Services
OPEN_METEO_API_KEY=your_weather_api_key
OSRM_SERVER_URL=http://localhost:5000

# Monitoring
GRAFANA_ADMIN_PASSWORD=your_grafana_password
SEQ_ADMIN_PASSWORD=your_seq_password
```

### Code Quality

```bash
# Format code
dotnet format

# Run static analysis
dotnet build --verbosity normal

# Run all tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
```

## 🔍 Troubleshooting

### Common Issues

#### Database Connection Issues
```bash
# Check if PostgreSQL container is running
docker ps | grep postgres

# Check database logs
docker logs fieldopsoptimizer_postgres_1

# Test connection manually
docker exec -it fieldopsoptimizer_postgres_1 psql -U fieldops_user -d fieldops_db
```

#### Port Conflicts
If you encounter port conflicts:
- API (5001): Check for other applications using this port
- PostgreSQL (5432): Modify port in `docker-compose.core.yml`
- Redis (6379): Modify port in `docker-compose.core.yml`

#### Memory Issues
For development on resource-constrained machines:
```powershell
# Start only core services
.\start-services.ps1 -Core

# Skip memory-intensive services like OSRM initially
```

#### Build Issues
```bash
# Clean solution
dotnet clean

# Restore packages
dotnet restore

# Rebuild
dotnet build
```

### Logs and Debugging

- **Application Logs**: Available in Seq at http://localhost:5341
- **Container Logs**: `docker logs <container_name>`
- **Database Logs**: Check PostgreSQL container logs
- **API Logs**: Located in `src/FieldOpsOptimizer.Api/logs/`

## 🤝 Contributing

### Development Workflow

1. **Fork the repository**
2. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes**
4. **Add tests** for new functionality
5. **Ensure all tests pass**
   ```bash
   dotnet test
   ```
6. **Follow coding standards**
   ```bash
   dotnet format
   ```
7. **Submit a Pull Request**

### Coding Standards

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Write unit tests for new features
- Follow SOLID principles
- Use async/await for I/O operations

### Architecture Guidelines

- **Domain Layer**: Pure business logic, no dependencies on infrastructure
- **Application Layer**: Use cases and orchestration, depends only on Domain
- **Infrastructure Layer**: External concerns, implements Application interfaces
- **API Layer**: HTTP concerns, thin controllers using MediatR

### Pull Request Guidelines

- Provide a clear description of changes
- Include tests for new functionality
- Update documentation if needed
- Ensure CI checks pass
- Keep PRs focused and reasonably sized

## 📊 Performance & Monitoring

### Key Metrics

- **API Response Times**: <200ms for CRUD operations
- **Route Optimization**: <5s for 50 jobs/10 technicians
- **Database Queries**: <100ms average
- **Memory Usage**: <512MB baseline

### Monitoring Stack

- **Metrics**: Prometheus + Grafana dashboards
- **Tracing**: Jaeger for distributed tracing
- **Logging**: Seq for centralized log aggregation
- **Health Checks**: Built-in ASP.NET Core health checks

### Performance Tips

- Enable Redis caching for frequently accessed data
- Use pagination for large datasets
- Implement database query optimization
- Monitor memory usage during route optimization

## 🔒 Security

### Security Measures

- Input validation and sanitization
- SQL injection prevention via Entity Framework
- CORS policy configuration
- Environment-specific configuration
- Secrets management (planned)

### Reporting Security Issues

If you discover a security vulnerability, please send an email to [security@example.com] instead of opening a public issue.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🚀 Deployment

### Production Considerations

- Use environment-specific configuration
- Enable HTTPS/TLS encryption
- Implement proper logging and monitoring
- Set up automated backups for PostgreSQL
- Configure load balancing if needed
- Use a reverse proxy (nginx/Apache)

### Docker Production

```bash
# Build production images
docker-compose -f docker-compose.yml -f docker-compose.prod.yml build

# Deploy with production configuration
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

## 📞 Support

- **Documentation**: See the [docs/](docs/) directory
- **Issues**: GitHub Issues for bug reports and feature requests
- **Discussions**: GitHub Discussions for questions and ideas

---

**Built with ❤️ by the Field Operations Team**
