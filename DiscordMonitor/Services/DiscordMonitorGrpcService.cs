using System.Collections.Concurrent;
using DiscordChatExporter.Core.Discord;
using DiscordMonitor.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace DiscordMonitor.Services;

public class DiscordMonitorGrpcService : Grpc.DiscordMonitorService.DiscordMonitorServiceBase
{
    private readonly IDiscordMonitorService _monitorService;
    private readonly ILogger<DiscordMonitorGrpcService> _logger;
    private readonly ConcurrentDictionary<IServerStreamWriter<DiscordMessage>, object> _connectedClients = new();

    // Track channel statistics
    private readonly ConcurrentDictionary<string, ChannelStats> _channelStats = new();

    private class ChannelStats
    {
        public string ChannelName { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTimeOffset LastCheck { get; set; }
        public int TotalMessagesProcessed { get; set; }
    }

    public DiscordMonitorGrpcService(
        IDiscordMonitorService monitorService,
        ILogger<DiscordMonitorGrpcService> logger)
    {
        _monitorService = monitorService;
        _logger = logger;

        // Subscribe to monitor service events
        _monitorService.OnNewMessage += async (sender, message) =>
        {
            var grpcMessage = new DiscordMessage
            {
                MessageId = message.MessageId.ToString(),
                ChannelId = message.ChannelId.ToString(),
                AuthorId = message.AuthorId.ToString(),
                AuthorName = message.AuthorName,
                Content = message.Content,
                Timestamp = Timestamp.FromDateTimeOffset(message.Timestamp)
            };

            // Update channel stats
            if (_channelStats.TryGetValue(message.ChannelId.ToString(), out var stats))
            {
                stats.LastCheck = DateTimeOffset.UtcNow;
                stats.TotalMessagesProcessed++;
            }

            // Broadcast to all connected clients
            foreach (var client in _connectedClients.Keys)
            {
                try
                {
                    await client.WriteAsync(grpcMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send message to a client");
                }
            }
        };
    }

    public override async Task<Empty> AddConfiguration(AddConfigurationRequest request, ServerCallContext context)
    {
        try
        {
            var config = new DiscordChannelConfig
            {
                Token = request.Token,
                ChannelId = Snowflake.Parse(request.ChannelId),
                FollowedUserIds = request.FollowedUserIds
                    .Select(Snowflake.Parse)
                    .ToList(),
                PollingInterval = request.PollingInterval.ToTimeSpan()
            };

            await _monitorService.AddConfigurationAsync(config, context.CancellationToken);

            // Initialize channel stats
            _channelStats[request.ChannelId] = new ChannelStats
            {
                IsActive = true,
                LastCheck = DateTimeOffset.UtcNow
            };

            // Try to get channel name
            try
            {
                var client = new DiscordClient(request.Token);
                var channel = await client.GetChannelAsync(config.ChannelId, context.CancellationToken);
                _channelStats[request.ChannelId].ChannelName = channel.Name;
            }
            catch
            {
                _channelStats[request.ChannelId].ChannelName = "Unknown";
            }

            return new Empty();
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<Empty> RemoveConfiguration(RemoveConfigurationRequest request, ServerCallContext context)
    {
        try
        {
            var channelId = Snowflake.Parse(request.ChannelId);
            await _monitorService.RemoveConfigurationAsync(channelId, context.CancellationToken);

            // Update channel stats
            if (_channelStats.TryGetValue(request.ChannelId, out var stats))
            {
                stats.IsActive = false;
            }

            return new Empty();
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task SubscribeToMessages(
        Empty request,
        IServerStreamWriter<DiscordMessage> responseStream,
        ServerCallContext context)
    {
        // Add client to broadcast list
        _connectedClients.TryAdd(responseStream, null!);

        try
        {
            // Keep the connection alive until the client disconnects
            await Task.Delay(-1, context.CancellationToken);
        }
        finally
        {
            // Remove client from broadcast list
            _connectedClients.TryRemove(responseStream, out _);
        }
    }

    public override Task<MonitoringStatus> GetMonitoringStatus(Empty request, ServerCallContext context)
    {
        var response = new MonitoringStatus();

        foreach (var (channelId, stats) in _channelStats)
        {
            response.Channels.Add(new ChannelStatus
            {
                ChannelId = channelId,
                ChannelName = stats.ChannelName,
                IsActive = stats.IsActive,
                LastCheck = Timestamp.FromDateTimeOffset(stats.LastCheck),
                TotalMessagesProcessed = stats.TotalMessagesProcessed
            });
        }

        return Task.FromResult(response);
    }
}
