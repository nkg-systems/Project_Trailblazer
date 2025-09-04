using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FieldOpsOptimizer.Web;
using FieldOpsOptimizer.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient with base address (will use localhost:5012 when API is running)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5012") });

// Register application services
builder.Services.AddScoped<DashboardService>();

await builder.Build().RunAsync();
