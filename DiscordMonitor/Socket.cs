using DiscordChatExporter.Core.Discord;
namespace DiscordMonitor;

using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(DiscordIdentify))]
[JsonSerializable(typeof(DiscordHeartbeat))]
[JsonSerializable(typeof(DiscordWebSocketMessage))]
internal partial class DiscordJsonContext : JsonSerializerContext
{
}

internal class DiscordIdentify
{
    [JsonPropertyName("op")]
    public int Op { get; set; } = 2;

    [JsonPropertyName("d")]
    public DiscordIdentifyData Data { get; set; } = new();
}

internal class DiscordIdentifyData
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("intents")]
    public int Intents { get; set; } = 33280; // GUILD_MESSAGES | DIRECT_MESSAGES | MESSAGE_CONTENT

    [JsonPropertyName("properties")]
    public DiscordIdentifyProperties Properties { get; set; } = new();
}

internal class DiscordIdentifyProperties
{
    [JsonPropertyName("os")]
    public string Os { get; set; } = "linux";

    [JsonPropertyName("browser")]
    public string Browser { get; set; } = "chrome";

    [JsonPropertyName("device")]
    public string Device { get; set; } = "chrome";
}

internal class DiscordHeartbeat
{
    [JsonPropertyName("op")]
    public int Op { get; set; } = 1;

    [JsonPropertyName("d")]
    public object? Data { get; set; } = null;
}

internal class DiscordWebSocketMessage
{
    [JsonPropertyName("op")]
    public int Op { get; set; }

    [JsonPropertyName("t")]
    public string? Type { get; set; }

    [JsonPropertyName("d")]
    public DiscordMessageData? Data { get; set; }
}

internal class DiscordMessageData
{
    [JsonPropertyName("channel_id")]
    public Snowflake? ChannelId { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("author")]
    public DiscordAuthor? Author { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("heartbeat_interval")]
    public int? HeartbeatInterval { get; set; }
}

internal class DiscordAuthor
{
    [JsonPropertyName("id")]
    public Snowflake? Id { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}
