namespace DiscordMonitor;

public class UserInfo
{
  public required string Id { get; init; }
  public required string Name { get; init; }
  public required string DisplayName { get; init; }
  public required bool IsBot { get; init; }
  public string? AvatarUrl { get; init; }
}
