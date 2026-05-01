namespace API.Services;

using System.Net.WebSockets;
using Google.Protobuf;
using Serilog;
using API.Protocol;

/// <summary>
/// Manages WebSocket connections and message processing for the simulation protocol.
/// Also periodically broadcasts simulation snapshots to connected clients.
/// </summary>
public class SimulationWebSocketService(
    SnapshotHandler snapshotHandler) : IEventSender
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
                    Log.Information("Snapshot loop cancelled");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in snapshot loop");
                    throw;
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
            Log.Warning("No open WebSocket client to send message");
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
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send message over WebSocket");
            throw;
        }
    }

    private static async Task ProcessConnectionAsync(WebSocket webSocket, CancellationToken cancelToken)
    {
        var buffer = new byte[_bufferSize];

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancelToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                await using var ms = new MemoryStream();

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
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("WebSocket connection cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing WebSocket connection");
            throw;
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
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("Snapshot loop cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in snapshot loop");
            throw;
        }
    }
}
