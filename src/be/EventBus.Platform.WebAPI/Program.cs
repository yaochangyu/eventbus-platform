using EventBus.Infrastructure.Extensions;
using EventBus.Infrastructure.TraceContext;
using EventBus.Infrastructure.Caching;
using EventBus.Infrastructure.Queue;
using EventBus.Platform.WebAPI.Middleware;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json.Serialization;
using EventBus.Infrastructure;

// Configure Serilog with structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "EventBus.Platform.WebAPI")
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/eventbus-platform-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: 
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "EventBus Platform API", 
        Version = "v1",
        Description = "EventBus Platform 的 Web API，提供事件管理和任務處理功能。"
    });
});

// Add HttpContextAccessor and HttpClient
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Configure Cache Options
builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection(CacheOptions.SectionName));

// Configure Queue Options
builder.Services.Configure<QueueOptions>(
    builder.Configuration.GetSection(QueueOptions.SectionName));

// Add Infrastructure services
builder.Services.AddInfrastructure();

// Add Repository services
builder.Services.AddScoped<EventBus.Platform.WebAPI.Repositories.IEventRepository, EventBus.Platform.WebAPI.Repositories.EventRepository>();
builder.Services.AddScoped<EventBus.Platform.WebAPI.Repositories.ITaskRepository, EventBus.Platform.WebAPI.Repositories.TaskRepository>();

// Add Service utilities
builder.Services.AddSingleton<IIdGenerator, GuidIdGenerator>();

// Add Handler services
builder.Services.AddScoped<EventBus.Platform.WebAPI.Handlers.ITaskHandler, EventBus.Platform.WebAPI.Handlers.TaskHandler>();
builder.Services.AddScoped<EventBus.Platform.WebAPI.Handlers.ITaskConfigHandler, EventBus.Platform.WebAPI.Handlers.TaskConfigHandler>();

// Note: TaskWorkerService has been moved to EventBus.Platform.TaskWorker Console Application

// Add Entity Framework with InMemory Database
builder.Services.AddDbContext<EventBusDbContext>(options =>
    options.UseInMemoryDatabase("EventBusDb"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EventBus Platform API v1");
        c.RoutePrefix = "swagger";
    });
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// 中介軟體管線順序很重要：
// 1. ExceptionHandlingMiddleware - 最外層，捕捉所有系統例外
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. TraceContextMiddleware - 處理追蹤內容與使用者身分驗證
app.UseMiddleware<TraceContextMiddleware>();

// 3. RequestParameterLoggerMiddleware - 記錄請求完成時的資訊
app.UseMiddleware<RequestParameterLoggerMiddleware>();

app.UseRouting();
app.MapControllers();

try
{
    Log.Information("Starting EventBus Platform API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "EventBus Platform API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Basic DbContext for MVP
public class EventBusDbContext(DbContextOptions<EventBusDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
