namespace API.Services;

using System.Net.WebSockets;
using Google.Protobuf;
using Protocol;

/// <summary>
/// Handles protobuf envelope serialization and deserialization.
/// </summary>
public partial class EnvelopeWebSocketHandler(
    ILogger<EnvelopeWebSocketHandler> logger)
{
    private readonly ILogger<EnvelopeWebSocketHandler> _logger = logger;

    /// <summary>
    /// Serialize and send an Envelope to the WebSocket.
    /// </summary>
    public async Task SendAsync(Envelope envelope, WebSocket webSocket, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = envelope.ToByteArray();
            await webSocket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                true,
                cancellationToken);

            LogEnvelopeSent(envelope.PayloadCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending envelope");
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sent envelope: {PayloadCase}")]
    private partial void LogEnvelopeSent(Envelope.PayloadOneofCase payloadCase);
}
