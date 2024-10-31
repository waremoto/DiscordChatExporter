namespace DiscordMonitor;

public class GuildInfo
{
  public required string Id { get; init; }
  public required string Name { get; init; }
  public string? IconUrl { get; init; }
}
