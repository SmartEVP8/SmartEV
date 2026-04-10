namespace API;

using API.Services;
using System.Net.WebSockets;
using Protocol;

/// <summary>
/// Defines WebSocket endpoints for the simulation protocol.
/// </summary>
public static class SimulationWebSocketEndpoints
{
    /// <summary>
    /// Maps the WebSocket endpoint for simulation protocol.
    /// </summary>
    /// <param name="app">The web application builder.</param>
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
            var engineService = context.RequestServices.GetRequiredService<SimulationEngineService>();
            var messageProcessor = context.RequestServices.GetRequiredService<SimulationMessageHandler>();
            var envelopeHandler = context.RequestServices.GetRequiredService<EnvelopeWebSocketHandler>();
            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SimulationWebSocket");

            // Attach client for event broadcasting
            engineService.AttachClient(webSocket);

            // Process the entire connection lifecycle
            var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            try
            {
                await ProcessWebSocketConnectionAsync(webSocket, messageProcessor, engineService, logger, cts.Token);
            }
            finally
            {
                engineService.DetachClient();
                webSocket.Dispose();
            }
        });
    }

    private static async Task ProcessWebSocketConnectionAsync(
        WebSocket webSocket,
        SimulationMessageHandler messageProcessor,
        SimulationEngineService engineService,
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
                }
                while (!result.EndOfMessage);

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

                        await RouteMessageAsync(envelope, messageProcessor, engineService, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing message");
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

    private static async Task RouteMessageAsync(
        Envelope envelope,
        SimulationMessageHandler messageProcessor,
        SimulationEngineService engineService,
        CancellationToken cancellationToken)
    {
        switch (envelope.PayloadCase)
        {
            case Envelope.PayloadOneofCase.Init when envelope.Init != null:
                messageProcessor.HandleInitRequest(envelope.Init);
                break;

            case Envelope.PayloadOneofCase.GetStationSnapshot when envelope.GetStationSnapshot != null:
                await engineService.OnGetStationSnapshot(envelope.GetStationSnapshot, cancellationToken);
                break;
        }
    }
}
