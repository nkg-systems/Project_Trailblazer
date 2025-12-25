# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["FieldOpsOptimizer.sln", "./"]
COPY ["src/FieldOpsOptimizer.Web/FieldOpsOptimizer.Web.csproj", "src/FieldOpsOptimizer.Web/"]

# Restore dependencies
RUN dotnet restore "src/FieldOpsOptimizer.Web/FieldOpsOptimizer.Web.csproj"

# Copy the rest of the code
COPY . .

# Build and publish the application
WORKDIR "/src/src/FieldOpsOptimizer.Web"
RUN dotnet publish "FieldOpsOptimizer.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - use nginx to serve the Blazor WASM app
FROM nginx:alpine AS runtime
WORKDIR /usr/share/nginx/html

# Remove default nginx content
RUN rm -rf ./*

# Copy published Blazor WASM files
COPY --from=build /app/publish/wwwroot .

# Copy nginx configuration
COPY src/FieldOpsOptimizer.Web/nginx.conf /etc/nginx/nginx.conf

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
