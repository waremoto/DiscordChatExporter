using DiscordChatExporter.Core.Discord;
namespace DiscordMonitor;

public interface IDiscordMonitorService
{
  Task AddConfigurationAsync(DiscordChannelConfig config, CancellationToken cancellationToken = default(CancellationToken));
  Task RemoveConfigurationAsync(Snowflake channelId, CancellationToken cancellationToken = default(CancellationToken));
  event EventHandler<DiscordChannelMessage> OnNewMessage;
}
