using Microsoft.AspNetCore.Server.Kestrel.Core;

using Serilog;

using Storage.Api.Middlewares;
using Storage.Application;
using Storage.Application.Configuration;
using Storage.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register health check services
builder.Services.AddHealthChecks();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
Log.Information("Configuration is starting...");

builder.Host.UseSerilog();

// Add Kestrel server options to allow large file uploads (up to 50 MB)
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = 50L * 1024 * 1024); // 50 MB

// Add Application services
var indexingSettings = IndexingSettings.BindFromConfiguration(builder.Configuration);
builder.Services.AddApplicationServices(indexingEnabled: indexingSettings.Enabled);

// Add Infrastructure (provider selection + encryption + indexing)
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin();
        policyBuilder.AllowAnyMethod();
        policyBuilder.AllowAnyHeader();
    });
});

// Add Swagger in Development environment only
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

// Expose a simple health endpoint at /health
app.MapHealthChecks("/health");

Log.Information("Application is starting...");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<LogMiddleware>();
app.MapControllers();

app.Run();
