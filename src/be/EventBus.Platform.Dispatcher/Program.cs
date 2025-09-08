using EventBus.Platform.Dispatcher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((context, config) =>
{
    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
    config.AddEnvironmentVariables();
});

builder.ConfigureServices((context, services) =>
{
    services.AddLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();
        logging.SetMinimumLevel(LogLevel.Information);
    });

    // HTTP Client for WebAPI communication
    services.AddHttpClient();
    
    // Remove local services - use HTTP API instead
    // services.AddSingleton<IQueueService, InMemoryQueueService>();
    // services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
    
    services.AddHostedService<DispatcherService>();
    // DemoTaskGenerator removed - use real task creation via API
    // TaskStatusMonitor removed - TaskWorker Console handles task execution
});

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("EventBus Platform Message Dispatcher starting...");

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Application terminated unexpectedly");
}
finally
{
    logger.LogInformation("EventBus Platform Message Dispatcher stopped");
}
