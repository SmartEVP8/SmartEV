using System.Net.WebSockets;
using API.Models;

namespace API.Services;

/// <summary>
/// Manages WebSocket connections and protobuf message broadcasting
/// </summary>
public interface IWebSocketService
{
    /// <summary>
    /// Handle an incoming WebSocket connection
    /// </summary>
    Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken);

    /// <summary>
    /// Broadcast a state snapshot to all connected clients
    /// </summary>
    Task BroadcastStateAsync(StateSnapShot snapshot);

    /// <summary>
    /// Broadcast an arrival event to all connected clients
    /// </summary>
    Task BroadcastArrivalAsync(ArriveAtStation arrival);

    /// <summary>
    /// Broadcast a charging completion event to all connected clients
    /// </summary>
    Task BroadcastChargingEndAsync(EndCharging charging);

    /// <summary>
    /// Send initial data to a specific client
    /// </summary>
    Task SendInitAsync(WebSocket webSocket, Init initData);

    /// <summary>
    /// Send station state to a specific client
    /// </summary>
    Task SendRequestStateAsync(WebSocket webSocket, RequestStationState state);

    /// <summary>
    /// Get the number of connected clients
    /// </summary>
    int GetConnectionCount();
}
