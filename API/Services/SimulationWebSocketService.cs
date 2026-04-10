namespace API.Services;

using System.Net.WebSockets;
using Google.Protobuf;
using Protocol;

/// <summary>
/// Manages WebSocket connections and message processing for the simulation protocol.
/// </summary>
public class SimulationWebSocketService(
    SnapshotHandler snapshotHandler,
    ILogger<SimulationWebSocketService> logger) : IEventSender
{
    private const int _bufferSize = 4096;

    private readonly ILogger<SimulationWebSocketService> _logger = logger;

    private readonly Lock _clientLock = new();

    private WebSocket? _client;

    /// <summary>
    /// Attaches a WebSocket client for event broadcasting. Only one client is supported at a time.
    /// </summary>
    /// <param name="socket">The WebSocket client to attach.</param>
    public void AttachClient(WebSocket socket)
    {
        lock (_clientLock)
        {
            _client = socket;
        }
    }

    /// <summary>
    /// Detaches the current WebSocket client, if any. Should be called when the client disconnects.
    /// </summary>
    public void DetachClient()
    {
        lock (_clientLock)
        {
            _client = null;
        }
    }

    private WebSocket? GetClient()
    {
        lock (_clientLock)
        {
            return _client?.State == WebSocketState.Open ? _client : null;
        }
    }

    /// <summary>
    /// Handles an incoming WebSocket connection.
    /// </summary>
    /// <param name="webSocket">The WebSocket connection.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that handles the connection.</returns>
    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancelToken)
    {
        AttachClient(webSocket);

        try
        {
            await ProcessConnectionAsync(webSocket, cancelToken);
        }
        finally
        {
            DetachClient();
            webSocket.Dispose();
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
            _logger.LogInformation("WebSocket connection cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WebSocket connection");
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
            _logger.LogError(ex, "Error processing message");
        }
    }

    /// <inheritdoc/>
    public async Task SendAsync(Envelope envelope, CancellationToken cancelToken = default)
    {
        var client = GetClient();
        if (client == null)
        {
            return;
        }

        try
        {
            var data = envelope.ToByteArray();
            await client.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                true,
                cancelToken);

            _logger.LogDebug("Sent envelope: {PayloadCase}", envelope.PayloadCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending envelope to client");
            DetachClient();
        }
    }

    private async Task RouteMessageAsync(Envelope envelope, CancellationToken cancelToken)
    {
        switch (envelope.PayloadCase)
        {
            case Envelope.PayloadOneofCase.GetStationSnapshot when envelope.GetStationSnapshot != null:
                var response = snapshotHandler.BuildStationSnapshot(envelope.GetStationSnapshot);
                await SendAsync(response, cancelToken);
                break;

            default:
                _logger.LogWarning("Unknown message type: {PayloadCase}", envelope.PayloadCase);
                break;
        }
    }
}
