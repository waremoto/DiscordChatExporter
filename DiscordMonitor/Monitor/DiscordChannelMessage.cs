using DiscordChatExporter.Core.Discord;
namespace DiscordMonitor;

public class DiscordChannelMessage
{
  public required string Content { get; set; }
  public required string AuthorName { get; set; }
  public required string AuthorId { get; set; }
  public required DateTimeOffset Timestamp { get; set; }
  public required string MessageId { get; set; }
  public required string ChannelId { get; set; }
}
