namespace API;

using API.Services;
using System.Net.WebSockets;
using Smartev.Api.V1;

/// <summary>
/// Defines WebSocket endpoints for the simulation protocol.
/// </summary>
public static class SimulationWebSocketEndpoints
{
    public static void MapSimulationWebSocket(this WebApplication app)
    {
        // Health check endpoint (simple HTTP GET)
        app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow })
            .WithName("HealthCheck")
            .Produces<object>();

        // Main WebSocket endpoint for simulation protocol
        app.Map("/ws/simulation", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var handler = context.RequestServices.GetRequiredService<EnvelopeWebSocketHandler>();
            var messageProcessor = context.RequestServices.GetRequiredService<IEnvelopeMessageHandler>();
            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SimulationWebSocket");

            // Process the entire connection lifecycle
            var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            await ProcessWebSocketConnectionAsync(webSocket, handler, messageProcessor, logger, cts.Token);
        });
    }

    private static async Task ProcessWebSocketConnectionAsync(
        WebSocket webSocket,
        EnvelopeWebSocketHandler handler,
        IEnvelopeMessageHandler messageProcessor,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                using var ms = new MemoryStream();
                // Read complete message
                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        cancellationToken);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    try
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        var envelope = Envelope.Parser.ParseFrom(ms.ToArray());

                        // Route based on payload type
                        var responseEnvelope = await RouteMessageAsync(envelope, messageProcessor, cancellationToken);

                        if (responseEnvelope != null)
                        {
                            await handler.SendEnvelopeAsync(responseEnvelope, webSocket, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing message");

                        // Send error response
                        var errorResponse = new Envelope
                        {
                            Error = new ErrorResponse
                            {
                                Code = 400,
                                Message = "Failed to process message",
                            },
                        };

                        await handler.SendEnvelopeAsync(errorResponse, webSocket, cancellationToken);
                    }
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

    private static async Task<Envelope?> RouteMessageAsync(
        Envelope envelope,
        IEnvelopeMessageHandler messageProcessor,
        CancellationToken cancellationToken)
    {
        return envelope.PayloadCase switch
        {
            Envelope.PayloadOneofCase.Init when envelope.Init is { } initReq
                => await messageProcessor.HandleInitRequestAsync(initReq, cancellationToken),

            Envelope.PayloadOneofCase.GetSnapshot when envelope.GetSnapshot is { } snapshotReq
                => await messageProcessor.HandleGetSnapshotRequestAsync(snapshotReq, cancellationToken),

            _ => new Envelope
            {
                Error = new ErrorResponse
                {
                    Code = 400,
                    Message = $"Unknown message type: {envelope.PayloadCase}",
                },
            },
        };
    }
}
