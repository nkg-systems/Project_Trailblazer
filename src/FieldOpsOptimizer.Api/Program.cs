using Microsoft.EntityFrameworkCore;
using FieldOpsOptimizer.Infrastructure.Data;
using FieldOpsOptimizer.Application.Common.Interfaces;
using FieldOpsOptimizer.Infrastructure.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
        "Host=localhost;Database=fieldops_optimizer;Username=fieldops_user;Password=fieldops_password"));

// Register repositories and services
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

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
