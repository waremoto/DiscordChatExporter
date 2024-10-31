using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordMonitor;

public class DiscordWebSocketMonitor : IAsyncDisposable
{
    private readonly ILogger<DiscordWebSocketMonitor> _logger;
    private readonly Dictionary<string, ChannelMonitorInfo> _monitoredChannels = new();
    private ClientWebSocket? _webSocket;
    private readonly CancellationTokenSource _backgroundCts = new();
    private Task? _receiveTask;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        TypeInfoResolver = DiscordJsonContext.Default
    };

    private class ChannelMonitorInfo
    {
        public required string Token { get; init; }
        public required string ChannelId { get; init; }
        public HashSet<string>? FollowedUserIds { get; init; }
    }

    public event EventHandler<DiscordChannelMessage>? OnNewMessage;

    public DiscordWebSocketMonitor(ILogger<DiscordWebSocketMonitor>? logger = null)
    {
        _logger = logger ?? NullLogger<DiscordWebSocketMonitor>.Instance;
    }

    public async Task AddConfigurationAsync(
        string token,
        string channelId,
        IEnumerable<string>? followedUserIds = null,
        CancellationToken cancellationToken = default)
    {
        _monitoredChannels[channelId] = new ChannelMonitorInfo
        {
            Token = token,
            ChannelId = channelId,
            FollowedUserIds = followedUserIds?.ToHashSet()
        };

        await EnsureWebSocketConnection(cancellationToken);
        await SubscribeToChannel(channelId, cancellationToken);
    }

    public async Task RemoveConfigurationAsync(
        string channelId,
        CancellationToken cancellationToken = default)
    {
        if (_monitoredChannels.Remove(channelId) && _webSocket?.State == WebSocketState.Open)
        {
            await UnsubscribeFromChannel(channelId, cancellationToken);
        }
    }

    private async Task EnsureWebSocketConnection(CancellationToken cancellationToken)
    {
        if (_webSocket?.State == WebSocketState.Open)
            return;

        _webSocket = new ClientWebSocket();
        await _webSocket.ConnectAsync(
            new Uri("wss://gateway.discord.gg/?v=9&encoding=json"),
            cancellationToken
        );

        _receiveTask = ReceiveMessagesAsync(_backgroundCts.Token);
    }

    private async Task SubscribeToChannel(string channelId, CancellationToken cancellationToken)
    {
        if (_webSocket?.State != WebSocketState.Open || !_monitoredChannels.TryGetValue(channelId, out var info))
            return;

        var identify = new DiscordIdentify
        {
            Data = new DiscordIdentifyData
            {
                Token = info.Token,
                Intents = 33280
            }
        };

        var identifyJson = JsonSerializer.Serialize(identify, JsonOptions);
        var identifyBytes = Encoding.UTF8.GetBytes(identifyJson);
        await _webSocket.SendAsync(
            identifyBytes,
            WebSocketMessageType.Text,
            true,
            cancellationToken
        );

        _logger.LogDebug("Sent websocket message: {Message}", identifyJson);
    }

    private Task UnsubscribeFromChannel(string channelId, CancellationToken cancellationToken)
    {
        // No explicit unsubscribe needed
        return Task.CompletedTask;
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var completeMessage = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken
                );

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                completeMessage.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    await HandleWebSocketMessage(completeMessage.ToString(), cancellationToken);
                    completeMessage.Clear();
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error receiving websocket messages");
            await Task.Delay(5000, cancellationToken);
            await ReconnectAsync(cancellationToken);
        }
    }

    private Task HandleWebSocketMessage(string message, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Received websocket message: {Message}", message);
            var wsMessage = JsonSerializer.Deserialize<DiscordWebSocketMessage>(message, JsonOptions);
            if (wsMessage == null) return Task.CompletedTask;

            switch (wsMessage.Op)
            {
                case 0 when wsMessage.Type == "MESSAGE_CREATE" && wsMessage.Data != null:
                    var data = wsMessage.Data;

                    string? channelId = data.ChannelId?.ToString();
                    string? authorId = data.Author?.Id.ToString();

                    if (channelId != null && data.Author != null &&
                        _monitoredChannels.TryGetValue(channelId, out var channelInfo))
                    {
                        if (channelInfo == null || authorId == null)
                            return Task.CompletedTask;


                        if (channelInfo.FollowedUserIds == null || authorId != null && channelInfo.FollowedUserIds.Contains(authorId))
                        {
                            var channelMessage = new DiscordChannelMessage
                            {
                                    MessageId = data.Id ?? "",
                                    ChannelId = channelId,
                                    Content = data.Content,
                                    AuthorId = authorId,
                                    AuthorName = data.Author?.Username ?? "Unknown",
                                    Timestamp = DateTimeOffset.Parse(data.Timestamp ?? DateTimeOffset.UtcNow.ToString("O"))
                            };

                            OnNewMessage?.Invoke(this, channelMessage);
                        }
                    }
                    break;

                case 10 when wsMessage.Data?.HeartbeatInterval != null:
                    _ = StartHeartbeatAsync(wsMessage.Data.HeartbeatInterval.Value, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling websocket message");
        }
        return Task.CompletedTask;
    }

    private async Task StartHeartbeatAsync(int interval, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var heartbeat = new DiscordHeartbeat();
                var heartbeatJson = JsonSerializer.Serialize(heartbeat, JsonOptions);
                var heartbeatBytes = Encoding.UTF8.GetBytes(heartbeatJson);

                await _webSocket.SendAsync(
                    heartbeatBytes,
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken
                );

                await Task.Delay(interval, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error in heartbeat");
        }
    }

    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_webSocket != null)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Reconnecting",
                    cancellationToken
                );
                _webSocket.Dispose();
                _webSocket = null;
            }

            foreach (var channelId in _monitoredChannels.Keys.ToList())
            {
                await EnsureWebSocketConnection(cancellationToken);
                await SubscribeToChannel(channelId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconnecting websocket");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _backgroundCts.Cancel();

            if (_webSocket != null)
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Disposing",
                    CancellationToken.None
                );
                _webSocket.Dispose();
            }

            _backgroundCts.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing websocket monitor");
        }
    }
}
