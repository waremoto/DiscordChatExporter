namespace DiscordMonitor;

public class Runner
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please specify which mode to run:");
            Console.WriteLine("  dotnet run -- client");
            Console.WriteLine("  dotnet run -- server");
            return;
        }

        switch (args[0].ToLower())
        {
            case "client":
                await RunClientMode();
                break;
            case "server":
                await RunServerMode();
                break;
            default:
                Console.WriteLine($"Unknown mode: {args[0]}");
                Console.WriteLine("Available modes:");
                Console.WriteLine("  client");
                Console.WriteLine("  server");
                break;
        }
    }

    private static async Task RunClientMode()
    {
        // Create logger factory for better debugging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddConsole();
        });
        var logger = loggerFactory.CreateLogger<DiscordWebSocketMonitor>();

        // Create websocket monitor
        await using var monitor = new DiscordWebSocketMonitor(logger);
        var cts = new CancellationTokenSource();

        try
        {
            // Subscribe to message events
            monitor.OnNewMessage += (sender, message) =>
            {
                Console.WriteLine(
                    $"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] - " +
                    $"[{message.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] - " +
                    $"[{message.MessageId}] {message.AuthorName}: {message.Content}"
                );
            };

            // Handle Ctrl+C
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Stopping client mode...");
                cts.Cancel();
                e.Cancel = true;
            };

            // Start monitoring the channel
            await monitor.AddConfigurationAsync(
                token: "token",
                channelId: "1174562361261162676",
                followedUserIds: null,
                cts.Token
            );

            Console.WriteLine("Successfully configured client mode");
            Console.WriteLine("Waiting for messages... (Press Ctrl+C to stop)");

            // Keep the application running until cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Client mode stopped");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in client mode: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner error: {ex.InnerException.Message}");
            }
        }
    }

    private static async Task RunServerMode()
    {
        // Create logger factory for server mode
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddConsole();
        });
        var logger = loggerFactory.CreateLogger<DiscordWebSocketMonitor>();

        var cts = new CancellationTokenSource();

        try
        {
            // Handle Ctrl+C
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Stopping server mode...");
                cts.Cancel();
                e.Cancel = true;
            };

            Console.WriteLine("Server mode started");
            Console.WriteLine("Running server operations... (Press Ctrl+C to stop)");

            // Keep the server running until cancellation
            try
            {
                // Add your server-specific logic here
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Server mode stopped");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in server mode: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner error: {ex.InnerException.Message}");
            }
        }
    }
}
