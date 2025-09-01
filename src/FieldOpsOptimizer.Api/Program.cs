using Microsoft.EntityFrameworkCore;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Infrastructure.Data.Repositories;
using FieldOpsOptimizer.Infrastructure.Optimization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure Database
if (builder.Environment.IsDevelopment())
{
    // Use in-memory database for testing
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
            throw new InvalidOperationException("Database connection string 'DefaultConnection' not found. Please configure your connection string in appsettings.json or environment variables.")));
}

// Register repositories and services
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register optimization services
builder.Services.AddScoped<IRouteOptimizationService, RouteOptimizationService>();
builder.Services.AddScoped<NearestNeighborOptimizer>();
builder.Services.AddScoped<TwoOptOptimizer>();
builder.Services.AddScoped<GeneticOptimizer>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
