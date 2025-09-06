using EventBus.Infrastructure.Caching;
using EventBus.Infrastructure.TraceContext;
using EventBus.Infrastructure.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraceContextType = EventBus.Infrastructure.TraceContext.TraceContext;

namespace EventBus.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Memory Cache 配置
        services.AddMemoryCache();
        services.AddSingleton<ICacheProvider, MemoryCacheProvider>();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        
        // TraceContext 服務
        services.AddScoped<IContextGetter<TraceContextType?>, TraceContextGetter>();
        
        // Queue 服務
        services.AddSingleton<IQueueService>(serviceProvider =>
        {
            var queueOptions = serviceProvider.GetService<IOptions<QueueOptions>>();
            var logger = serviceProvider.GetRequiredService<ILogger<ChannelQueueService>>();
            
            // 預設使用 Channel 實作
            var providerType = queueOptions?.Value.ProviderType ?? QueueProviderType.Channel;
            
            return providerType switch
            {
                QueueProviderType.Channel => new ChannelQueueService(logger),
                QueueProviderType.ConcurrentQueue => new ConcurrentQueueService(
                    serviceProvider.GetRequiredService<ILogger<ConcurrentQueueService>>()),
                _ => new ChannelQueueService(logger)
            };
        });
        
        return services;
    }
}