using Serilog;

using StorageService.Api.Middlewares;
using StorageService.Application;
using StorageService.Application.Errors;
using StorageService.Application.Interfaces;
using StorageService.Infrastructure;

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

// Add Application services
builder.Services.AddApplicationServices();

// Add Storage Infrastructure (provider selection + error catalog)
builder.Services.AddStorageInfrastructure(builder.Configuration);

// Add Error Catalog Path
var path = Path.Combine(builder.Environment.ContentRootPath, "errors.json");
if (!File.Exists(path))
    throw new FileNotFoundException($"errors.json not found at: {path}");
Log.Information("Using error catalog at: {Path}", path);
var errorcat = ErrorCatalog.LoadFromFile(path);
builder.Services.AddSingleton<IErrorCatalog>(errorcat);

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
