namespace API;

using OpenApiUi;
using API.Models;
using API.Services;

public static class SimulationEndpoints
{
    public static void MapSimulationEndpoints(this WebApplication app)
    {
        // Health check endpoint
        app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow })
            .WithName("HealthCheck");

        // WebSocket endpoint for real-time streaming
        app.Map("/ws/simulation", async (HttpContext context, IWebSocketService wsService) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await wsService.HandleConnectionAsync(webSocket, CancellationToken.None);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        });

        // REST Endpoint: Initialize simulation
        app.MapPost("/api/simulation/initialize", async (
            Initialise initialisationData,
            ISimulationStateService stateService,
            IWebSocketService wsService) =>
        {
            var initData = new Init
            {
                // Map from Initialise to Init as needed
                // This would be populated based on your simulation engine
            };

            stateService.SetInitializationData(initData);

            return Results.Accepted(null, new
            {
                message = "Simulation initialized",
                connectedClients = wsService.GetConnectionCount()
            });
        })
        .WithName("InitializeSimulation");

        // REST Endpoint: Get current state
        app.MapGet("/api/simulation/state", (ISimulationStateService stateService) =>
        {
            var state = stateService.GetStationState();
            if (state == null)
            {
                return Results.NotFound();
            }
            return Results.Ok(state);
        })
        .WithName("GetSimulationState");

        // REST Endpoint: Get initialization data
        app.MapGet("/api/simulation/init", (ISimulationStateService stateService) =>
        {
            var init = stateService.GetInitializationData();
            if (init == null)
            {
                return Results.NotFound();
            }
            return Results.Ok(init);
        })
        .WithName("GetInitilizationData");

        // REST Endpoint: Get connected clients count
        app.MapGet("/api/simulation/clients", (IWebSocketService wsService) =>
        {
            return Results.Ok(new { connectedClients = wsService.GetConnectionCount() });
        })
        .WithName("GetConnectedClientsCount");

        // REST Endpoint: Broadcast state update
        app.MapPost("/api/simulation/broadcast-state", async (
            StateSnapShot snapshot,
            IWebSocketService wsService) =>
        {
            await wsService.BroadcastStateAsync(snapshot);
            return Results.Accepted(null, new { message = "State broadcast sent" });
        })
        .WithName("BroadcastState");

        // REST Endpoint: Broadcast arrival event
        app.MapPost("/api/simulation/broadcast-arrival", async (
            ArriveAtStation arrival,
            ISimulationStateService stateService,
            IWebSocketService wsService) =>
        {
            stateService.RecordArrival(arrival);
            await wsService.BroadcastArrivalAsync(arrival);
            return Results.Accepted(null, new { message = "Arrival event broadcast" });
        })
        .WithName("BroadcastArrival");

        // REST Endpoint: Broadcast charging end event
        app.MapPost("/api/simulation/broadcast-charging-end", async (
            EndCharging charging,
            ISimulationStateService stateService,
            IWebSocketService wsService) =>
        {
            stateService.RecordChargingEnd(charging);
            await wsService.BroadcastChargingEndAsync(charging);
            return Results.Accepted(null, new { message = "Charging end event broadcast" });
        })
        .WithName("BroadcastChargingEnd");

        // REST Endpoint: Clear simulation state
        app.MapPost("/api/simulation/reset", (ISimulationStateService stateService) =>
        {
            stateService.Clear();
            return Results.Ok(new { message = "Simulation state reset" });
        })
        .WithName("ResetSimulation");
    }
}