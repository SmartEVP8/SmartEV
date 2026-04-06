using System.Net.WebSockets;
using Google.Protobuf;
using Smartev.Api.V1;

namespace API.Services;

/// <summary>
/// Handles binary protobuf-based WebSocket communication.
/// For single-client sequential model, just handles serialization/deserialization.
/// </summary>
public class EnvelopeWebSocketHandler(
    ILogger<EnvelopeWebSocketHandler> logger)
{
    private readonly ILogger<EnvelopeWebSocketHandler> _logger = logger;

    /// <summary>
    /// Send an Envelope message to the WebSocket.
    /// </summary>
    public async Task SendEnvelopeAsync(Envelope envelope, WebSocket webSocket, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = envelope.ToByteArray();
            await webSocket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                true,
                cancellationToken);

            _logger.LogDebug("Sent envelope: {PayloadCase}", envelope.PayloadCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending envelope");
        }
    }
}
