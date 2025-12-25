# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["FieldOpsOptimizer.sln", "./"]
COPY ["src/FieldOpsOptimizer.Api/FieldOpsOptimizer.Api.csproj", "src/FieldOpsOptimizer.Api/"]
COPY ["src/FieldOpsOptimizer.Application/FieldOpsOptimizer.Application.csproj", "src/FieldOpsOptimizer.Application/"]
COPY ["src/FieldOpsOptimizer.Domain/FieldOpsOptimizer.Domain.csproj", "src/FieldOpsOptimizer.Domain/"]
COPY ["src/FieldOpsOptimizer.Infrastructure/FieldOpsOptimizer.Infrastructure.csproj", "src/FieldOpsOptimizer.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/FieldOpsOptimizer.Api/FieldOpsOptimizer.Api.csproj"

# Copy the rest of the code
COPY . .

# Build the application
WORKDIR "/src/src/FieldOpsOptimizer.Api"
RUN dotnet build "FieldOpsOptimizer.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "FieldOpsOptimizer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published files
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FieldOpsOptimizer.Api.dll"]
