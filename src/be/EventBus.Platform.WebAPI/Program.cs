using EventBus.Infrastructure.Extensions;
using EventBus.Infrastructure.TraceContext;
using EventBus.Infrastructure.Caching;
using EventBus.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json.Serialization;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/eventbus-platform-.log", rollingInterval: RollingInterval.Day)
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
builder.Services.AddScoped<EventBus.Platform.WebAPI.Repositories.ISchedulerTaskRepository, EventBus.Platform.WebAPI.Repositories.SchedulerTaskRepository>();
builder.Services.AddScoped<EventBus.Platform.WebAPI.Repositories.ISubscriptionRepository, EventBus.Platform.WebAPI.Repositories.SubscriptionRepository>();

// Add Handler services
builder.Services.AddScoped<EventBus.Platform.WebAPI.Handlers.ISchedulerHandler, EventBus.Platform.WebAPI.Handlers.SchedulerHandler>();
builder.Services.AddScoped<EventBus.Platform.WebAPI.Handlers.ITaskHandler, EventBus.Platform.WebAPI.Handlers.TaskHandler>();

// Note: TaskWorkerService has been moved to EventBus.Platform.TaskWorker Console Application

// Add Entity Framework with InMemory Database
builder.Services.AddDbContext<EventBusDbContext>(options =>
    options.UseInMemoryDatabase("EventBusDb"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseMiddleware<TraceContextMiddleware>();
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
