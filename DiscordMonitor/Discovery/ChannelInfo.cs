namespace DiscordMonitor;

public class ChannelInfo
{
  public required string Id { get; init; }
  public required string Name { get; init; }
  public required string Type { get; init; }
  public required string GuildName { get; init; }
  public string? ParentName { get; init; }
  public string? Topic { get; init; }
}
