using DiscordMonitor.Grpc;
namespace DiscordMonitor;

// Extension method for service registration
public static class DiscordMonitorServiceExtensions
{
    // public static IServiceCollection AddDiscordMonitor(this IServiceCollection services)
    // {
    //     services.AddSingleton<IDiscordMonitorService, DiscordMonitorService>();
    //     services.AddHostedService(sp => sp.GetRequiredService<IDiscordMonitorService>());
    //     return services;
    // }

    public static IServiceCollection AddDiscordWebSocketMonitor(this IServiceCollection services)
    {
        services.AddSingleton<DiscordWebSocketMonitor>();
        return services;
    }
}
