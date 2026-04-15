namespace API.Services;

using System.Net.WebSockets;
using Google.Protobuf;
using Protocol;

/// <summary>
/// Manages WebSocket connections and message processing for the simulation protocol.
/// Also periodically broadcasts simulation snapshots to connected clients.
/// </summary>
public class SimulationWebSocketService(
    SnapshotHandler snapshotHandler,
    ILogger<SimulationWebSocketService> logger) : IEventSender
{
    private const int _bufferSize = 4096;
    private const int _snapshotIntervalMs = 1000;

    private WebSocket? _client;

    /// <summary>
    /// Handles an incoming WebSocket connection.
    /// </summary>
    /// <param name="webSocket">The WebSocket connection.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that handles the connection.</returns>
    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancelToken)
    {
        _client = webSocket;
        using var snapshotCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

        Task? snapshotLoopTask = null;

        try
        {
            snapshotLoopTask = RunSnapshotLoopAsync(snapshotCts.Token);
            await ProcessConnectionAsync(webSocket, cancelToken);
        }
        finally
        {
            await snapshotCts.CancelAsync();

            if (snapshotLoopTask != null)
            {
                try
                {
                    await snapshotLoopTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error stopping snapshot loop");
                }
            }

            _client = null;

            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None);
            }

            webSocket.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task SendAsync(Envelope envelope, CancellationToken cancelToken = default)
    {
        if (_client?.State != WebSocketState.Open)
        {
            logger.LogWarning("No open WebSocket client to send message");
            return;
        }

        try
        {
            var data = envelope.ToByteArray();
            await _client.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                true,
                cancelToken);

            logger.LogDebug("Sent envelope: {PayloadCase}", envelope.PayloadCase);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending envelope to client");
        }
    }

    private async Task ProcessConnectionAsync(WebSocket webSocket, CancellationToken cancelToken)
    {
        var buffer = new byte[_bufferSize];

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancelToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                using var ms = new MemoryStream();

                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancelToken);
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        cancelToken);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    await ProcessMessageAsync(ms, cancelToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("WebSocket connection cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in WebSocket connection");
        }
    }

    private async Task ProcessMessageAsync(MemoryStream ms, CancellationToken cancelToken)
    {
        try
        {
            ms.Seek(0, SeekOrigin.Begin);
            var envelope = Envelope.Parser.ParseFrom(ms.ToArray());
            await RouteMessageAsync(envelope, cancelToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message");
        }
    }

    private async Task RouteMessageAsync(Envelope envelope, CancellationToken cancelToken)
    {
        switch (envelope.PayloadCase)
        {
            case Envelope.PayloadOneofCase.GetStationSnapshot when envelope.GetStationSnapshot != null:
                var response = snapshotHandler.BuildStationSnapshot(envelope.GetStationSnapshot.StationId);
                await SendAsync(response, cancelToken);
                break;
            default:
                logger.LogWarning("Unknown message type: {PayloadCase}", envelope.PayloadCase);
                break;
        }
    }

    private async Task RunSnapshotLoopAsync(CancellationToken cancelToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_snapshotIntervalMs));
            while (await timer.WaitForNextTickAsync(cancelToken))
            {
                var envelope = snapshotHandler.BuildSimulationSnapshot();
                await SendAsync(envelope, cancelToken);
                logger.LogDebug("Broadcast simulation snapshot");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Snapshot broadcast loop crashed");
        }
    }
}
