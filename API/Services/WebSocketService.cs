using System.Collections.Concurrent;
using System.Net.WebSockets;
using ProtoBuf;
using API.Models;

namespace API.Services;

public class WebSocketService : IWebSocketService
{
    private readonly ConcurrentDictionary<string, WebSocketClient> _clients = new();
    private readonly ILogger<WebSocketService> _logger;
    private int _clientIdCounter = 0;

    public WebSocketService(ILogger<WebSocketService> logger)
    {
        _logger = logger;
    }

    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var clientId = Interlocked.Increment(ref _clientIdCounter).ToString();
        var client = new WebSocketClient(clientId, webSocket);
        _clients.TryAdd(clientId, client);

        _logger.LogInformation("Client {ClientId} connected. Total clients: {ClientCount}", clientId, _clients.Count);

        try
        {
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Client can send binary messages if needed for control
                    _logger.LogDebug("Received binary message from client {ClientId}", clientId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client {ClientId} connection cancelled", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client {ClientId} connection", clientId);
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            webSocket.Dispose();
            _logger.LogInformation("Client {ClientId} disconnected. Total clients: {ClientCount}", clientId, _clients.Count);
        }
    }

    public async Task BroadcastStateAsync(StateSnapShot snapshot)
    {
        await BroadcastMessageAsync((message) =>
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, snapshot);
            return ms.ToArray();
        });
    }

    public async Task BroadcastArrivalAsync(ArriveAtStation arrival)
    {
        await BroadcastMessageAsync((message) =>
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, arrival);
            return ms.ToArray();
        });
    }

    public async Task BroadcastChargingEndAsync(EndCharging charging)
    {
        await BroadcastMessageAsync((message) =>
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, charging);
            return ms.ToArray();
        });
    }

    public async Task SendInitAsync(WebSocket webSocket, Init initData)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, initData);
            var data = ms.ToArray();

            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending init data");
            }
        }
    }

    public async Task SendRequestStateAsync(WebSocket webSocket, RequestStationState state)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, state);
            var data = ms.ToArray();

            try
            {
                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending request state");
            }
        }
    }

    public int GetConnectionCount() => _clients.Count;

    private async Task BroadcastMessageAsync(Func<string, byte[]> messageBuilder)
    {
        var tasks = new List<Task>();
        var messageType = "broadcast";
        var data = messageBuilder(messageType);

        foreach (var client in _clients.Values)
        {
            if (client.WebSocket.State == WebSocketState.Open)
            {
                tasks.Add(SendToClientAsync(client.WebSocket, data));
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    private async Task SendToClientAsync(WebSocket webSocket, byte[] data)
    {
        try
        {
            await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to client");
        }
    }
}

internal class WebSocketClient
{
    public string Id { get; }
    public WebSocket WebSocket { get; }
    public DateTime ConnectedAt { get; }

    public WebSocketClient(string id, WebSocket webSocket)
    {
        Id = id;
        WebSocket = webSocket;
        ConnectedAt = DateTime.UtcNow;
    }
}
