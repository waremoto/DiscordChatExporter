using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Utils.Extensions;
using System.Runtime.CompilerServices;
using Grpc.Net.Client;
using DiscordMonitor.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace DiscordMonitor;

public class DiscordMonitorClient : IAsyncDisposable
{
  private readonly Grpc.DiscordMonitorService.DiscordMonitorServiceClient _client;
  private readonly GrpcChannel _channel;
  private DiscordClient? _discordClient;

  public DiscordMonitorClient(string address = "https://localhost:5001")
  {
    _channel = GrpcChannel.ForAddress(address);
    _client = new Grpc.DiscordMonitorService.DiscordMonitorServiceClient(_channel);
  }

  private DiscordClient GetDiscordClient(string token)
  {
    return _discordClient ??= new DiscordClient(token);
  }

  // Discovery tools
  public async Task<IReadOnlyList<GuildInfo>> ListGuildsAsync(
      string token,
      CancellationToken cancellationToken = default)
  {
    var discord = GetDiscordClient(token);
    var guilds = new List<GuildInfo>();

    await foreach (var guild in discord.GetUserGuildsAsync(cancellationToken))
    {
      guilds.Add(new GuildInfo
      {
          Id = guild.Id.ToString(),
          Name = guild.Name,
          IconUrl = guild.IconUrl
      });
    }

    return guilds;
  }

  public async Task<IReadOnlyList<ChannelInfo>> ListGuildChannelsAsync(
      string token,
      string guildId,
      bool includeThreads = false,
      CancellationToken cancellationToken = default)
  {
    var discord = GetDiscordClient(token);
    var guildSnowflake = Snowflake.Parse(guildId);
    var channels = new List<ChannelInfo>();

    // Get the guild info
    var guild = await discord.GetGuildAsync(guildSnowflake, cancellationToken);

    // Get regular channels
    await foreach (var channel in discord.GetGuildChannelsAsync(guildSnowflake, cancellationToken))
    {
      channels.Add(new ChannelInfo
      {
          Id = channel.Id.ToString(),
          Name = channel.Name,
          Type = channel.Kind.ToString(),
          GuildName = guild.Name,
          ParentName = channel.Parent?.Name,
          Topic = channel.Topic
      });
    }

    // Get threads if requested
    if (includeThreads)
    {
      await foreach (var thread in discord.GetGuildThreadsAsync(guildSnowflake, true, null, null, cancellationToken))
      {
        channels.Add(new ChannelInfo
        {
            Id = thread.Id.ToString(),
            Name = thread.Name,
            Type = thread.Kind.ToString(),
            GuildName = guild.Name,
            ParentName = thread.Parent?.Name,
            Topic = thread.Topic
        });
      }
    }

    return channels;
  }

  public async Task<IReadOnlyList<ChannelInfo>> ListDirectMessageChannelsAsync(
      string token,
      CancellationToken cancellationToken = default)
  {
    var discord = GetDiscordClient(token);
    var channels = new List<ChannelInfo>();

    await foreach (var channel in discord.GetGuildChannelsAsync(Guild.DirectMessages.Id, cancellationToken))
    {
      channels.Add(new ChannelInfo
      {
          Id = channel.Id.ToString(),
          Name = channel.Name,
          Type = channel.Kind.ToString(),
          GuildName = "Direct Messages",
          ParentName = channel.Parent?.Name,
          Topic = channel.Topic
      });
    }

    return channels;
  }

  public async Task<IReadOnlyList<ChannelInfo>> ListAllChannelsAsync(
      string token,
      bool includeThreads = false,
      CancellationToken cancellationToken = default)
  {
    var channels = new List<ChannelInfo>();

    // Get DM channels
    channels.AddRange(await ListDirectMessageChannelsAsync(token, cancellationToken));

    // Get channels from all guilds
    var guilds = await ListGuildsAsync(token, cancellationToken);
    foreach (var guild in guilds.Where(g => g.Id != Guild.DirectMessages.Id.ToString()))
    {
      channels.AddRange(await ListGuildChannelsAsync(token, guild.Id, includeThreads, cancellationToken));
    }

    return channels;
  }

  public async Task<IReadOnlyList<UserInfo>> ListChannelUsersAsync(
      string token,
      string channelId,
      DateTimeOffset? after = null,
      DateTimeOffset? before = null,
      CancellationToken cancellationToken = default)
  {
    var discord = GetDiscordClient(token);
    var channelSnowflake = Snowflake.Parse(channelId);
    var users = new HashSet<UserInfo>();

    // Get messages in the specified time range
    await foreach (var message in discord.GetMessagesAsync(
        channelSnowflake,
        after?.Pipe(Snowflake.FromDate),
        before?.Pipe(Snowflake.FromDate),
        null,
        cancellationToken))
    {
      // Add message author
      users.Add(new UserInfo
      {
          Id = message.Author.Id.ToString(),
          Name = message.Author.Name,
          DisplayName = message.Author.DisplayName,
          IsBot = message.Author.IsBot,
          AvatarUrl = message.Author.AvatarUrl
      });

      // Add mentioned users
      foreach (var mentionedUser in message.MentionedUsers)
      {
        users.Add(new UserInfo
        {
            Id = mentionedUser.Id.ToString(),
            Name = mentionedUser.Name,
            DisplayName = mentionedUser.DisplayName,
            IsBot = mentionedUser.IsBot,
            AvatarUrl = mentionedUser.AvatarUrl
        });
      }
    }

    return users.ToList();
  }

  // Original monitoring methods
  public async Task AddConfigurationAsync(
      string token,
      string channelId,
      IEnumerable<string>? followedUserIds = null,
      TimeSpan? pollingInterval = null)
  {
    var request = new AddConfigurationRequest
    {
        Token = token,
        ChannelId = channelId,
        PollingInterval = Duration.FromTimeSpan(pollingInterval ?? TimeSpan.FromSeconds(30))
    };

    if (followedUserIds != null)
    {
      request.FollowedUserIds.AddRange(followedUserIds);
    }

    await _client.AddConfigurationAsync(request);
  }

  public async Task RemoveConfigurationAsync(string channelId)
  {
    var request = new RemoveConfigurationRequest
    {
        ChannelId = channelId
    };

    await _client.RemoveConfigurationAsync(request);
  }

  public async IAsyncEnumerable<DiscordMessage> SubscribeToMessagesAsync(
      [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    var request = new Empty();
    var call = _client.SubscribeToMessages(request);

    await foreach (var message in call.ResponseStream.ReadAllAsync(cancellationToken))
    {
      yield return message;
    }
  }

  public async Task<MonitoringStatus> GetMonitoringStatusAsync()
  {
    return await _client.GetMonitoringStatusAsync(new Empty());
  }

  public async ValueTask DisposeAsync()
  {
    await _channel.ShutdownAsync();
  }
}
public class ExampleUsage
{
  public static async Task Example()
  {
    var client = new DiscordMonitorClient();

    try
    {
      string token = "token";

      // List all guilds (servers)
      Console.WriteLine("Available Guilds:");
      var guilds = await client.ListGuildsAsync(token);
      foreach (var guild in guilds)
      {
        Console.WriteLine($"Guild: {guild.Name} (ID: {guild.Id})");

        // List channels in each guild
        var channels = await client.ListGuildChannelsAsync(token, guild.Id, includeThreads: true);
        foreach (var channel in channels)
        {
          Console.WriteLine($"  Channel: {channel.Name} (ID: {channel.Id}, Type: {channel.Type})");

          // List users who have participated in each channel in the last 7 days
          var users = await client.ListChannelUsersAsync(
              token,
              channel.Id,
              DateTimeOffset.UtcNow.AddDays(-7),
              DateTimeOffset.UtcNow
          );

          foreach (var user in users)
          {
            Console.WriteLine($"    User: {user.DisplayName} (ID: {user.Id})");
          }

          // List direct message channels
          Console.WriteLine("\nDirect Message Channels:");
          var dmChannels = await client.ListDirectMessageChannelsAsync(token);
          foreach (var channeldb in dmChannels)
          {
            Console.WriteLine($"DM Channel: {channeldb.Name} (ID: {channeldb.Id})");
          }

          // Start monitoring a specific channel
          string channelToMonitor = channels.First().Id;
          string[] usersToFollow = users.Select(u => u.Id).ToArray();

          await client.AddConfigurationAsync(
              token: token,
              channelId: channelToMonitor,
              followedUserIds: usersToFollow,
              pollingInterval: TimeSpan.FromSeconds(30)
          );

          // Subscribe to messages
          var cts = new CancellationTokenSource();
          await foreach (var message in client.SubscribeToMessagesAsync(cts.Token))
          {
            Console.WriteLine($"[{message.Timestamp}] {message.AuthorName}: {message.Content}");
          }
        }
      }

    }
    finally
    {
      await client.DisposeAsync();
    }
  }
}

// Example usage
public class ExampleUsage2
{
  public static async Task Example()
  {
    var client = new DiscordMonitorClient("https://localhost:5000");

    // Add a channel to monitor
    await client.AddConfigurationAsync(
        token: "token",
        channelId: "1174562361261162676",
        followedUserIds: null
    );

    // Subscribe to messages
    var cts = new CancellationTokenSource();
    await foreach (var message in client.SubscribeToMessagesAsync(cts.Token))
    {
      Console.WriteLine($"[{message.Timestamp}] {message.AuthorName}: {message.Content}");
    }

    // Get monitoring status
    var status = await client.GetMonitoringStatusAsync();
    foreach (var channel in status.Channels)
    {
      Console.WriteLine(
          $"Channel {channel.ChannelName}: " +
          $"Active={channel.IsActive}, " +
          $"Messages={channel.TotalMessagesProcessed}");
    }
  }
}
