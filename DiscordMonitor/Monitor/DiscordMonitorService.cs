// using System.Collections.Concurrent;
// using DiscordChatExporter.Core.Discord;
// using DiscordChatExporter.Core.Discord.Data;
// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
//
// namespace DiscordMonitor.Services;
//
// public class DiscordMonitorService : BackgroundService, IDiscordMonitorService, IAsyncDisposable
// {
//     private readonly ILogger<DiscordMonitorService> _logger;
//     private readonly ConcurrentDictionary<Snowflake, (DiscordChannelConfig Config, DiscordClient Client)> _configurations;
//     private readonly DiscordWebSocketMonitor _webSocketMonitor;
//     private readonly object _eventLock = new();
//
//     public event EventHandler<DiscordChannelMessage>? OnNewMessage;
//
//     public DiscordMonitorService(ILogger<DiscordMonitorService> logger)
//     {
//         _logger = logger;
//         _configurations = new ConcurrentDictionary<Snowflake, (DiscordChannelConfig, DiscordClient)>();
//         _webSocketMonitor = new DiscordWebSocketMonitor(logger);
//
//         // Forward websocket events
//         _webSocketMonitor.OnNewMessage += (sender, message) =>
//         {
//             lock (_eventLock)
//             {
//                 OnNewMessage?.Invoke(this, message);
//             }
//         };
//     }
//
//     public async Task AddConfigurationAsync(
//         DiscordChannelConfig config,
//         CancellationToken cancellationToken = default)
//     {
//         var client = new DiscordClient(config.Token);
//
//         // Verify the channel exists and is accessible
//         var channel = await client.GetChannelAsync(config.ChannelId, cancellationToken);
//
//         _configurations[config.ChannelId] = (config, client);
//
//         // Add to websocket monitor
//         await _webSocketMonitor.AddConfigurationAsync(
//             config.Token,
//             config.ChannelId.ToString(), // Convert Snowflake to string
//             config.FollowedUserIds?.Select(id => id.ToString()), // Convert Snowflake IDs to strings
//             cancellationToken
//         );
//
//         _logger.LogInformation("Added monitoring configuration for channel {ChannelId}", config.ChannelId);
//     }
//
//     public async Task RemoveConfigurationAsync(
//         Snowflake channelId,
//         CancellationToken cancellationToken = default)
//     {
//         if (_configurations.TryRemove(channelId, out _))
//         {
//             // Remove from websocket monitor
//             await _webSocketMonitor.RemoveConfigurationAsync(
//                 channelId.ToString(), // Convert Snowflake to string
//                 cancellationToken
//             );
//
//             _logger.LogInformation("Removed monitoring configuration for channel {ChannelId}", channelId);
//         }
//     }
//
//     protected override Task ExecuteAsync(CancellationToken stoppingToken)
//     {
//         // No background task needed as WebSocket handles real-time messages
//         return Task.CompletedTask;
//     }
//
//     public override async Task StopAsync(CancellationToken cancellationToken)
//     {
//         await _webSocketMonitor.DisposeAsync();
//         await base.StopAsync(cancellationToken);
//     }
//
//     public async ValueTask DisposeAsync()
//     {
//         await _webSocketMonitor.DisposeAsync();
//         GC.SuppressFinalize(this);
//     }
// }
//
// // Interface
// public interface IDiscordMonitorService
// {
//     Task AddConfigurationAsync(DiscordChannelConfig config, CancellationToken cancellationToken = default);
//     Task RemoveConfigurationAsync(Snowflake channelId, CancellationToken cancellationToken = default);
//     event EventHandler<DiscordChannelMessage> OnNewMessage;
// }
//
// // Configuration class
// public class DiscordChannelConfig
// {
//     public required string Token { get; init; }
//     public required Snowflake ChannelId { get; init; }
//     public List<Snowflake> FollowedUserIds { get; init; } = new();
//     public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(30); // Kept for backwards compatibility
// }
//
// // Message class
