using DiscordChatExporter.Core.Discord;
namespace DiscordMonitor;

public class DiscordChannelConfig
{
  public required string Token { get; set; }
  public required Snowflake ChannelId { get; set; }
  public List<Snowflake> FollowedUserIds { get; set; } = new();
  public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);
}
