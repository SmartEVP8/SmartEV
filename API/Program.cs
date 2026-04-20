namespace API;

using Services;
using Engine.Events;
using Engine.Cost;
using API.EngineManager;
using Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Core.Charging;

/// <summary>
/// Entry point for the SmartEV API application.
/// </summary>
public static class Program
{
    /// <summary>
    /// The main entry point for the SmartEV API application.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation that initializes the application.</returns>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<EngineManager.EngineManager>();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
                policy.WithOrigins("http://localhost:5173")
                      .AllowAnyMethod()
                      .AllowAnyHeader());
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
            app.UseDeveloperExceptionPage();

        app.UseWebSockets();
        app.UseCors("AllowFrontend");

        app.MapGet("/weights", async () => Results.Ok(CostWeightMetadata.All));

        app.MapPost("/init-engine", async (EngineManager.EngineManager engineManager, EngineInitConfigDTO config) =>
        {
            var result = await engineManager.InitializeAsync(config, services =>
            {
                services.AddSingleton(provider => app.Services.GetRequiredService<ILoggerFactory>());
                services.AddLogging();
                services.AddSingleton<IEngineEventSubscriber, EngineEventSubscriber>();
                services.AddSingleton<SnapshotHandler>();
                services.AddSingleton<SimulationWebSocketService>();
                services.AddSingleton<IEventSender>(sp => sp.GetRequiredService<SimulationWebSocketService>());
                services.AddSingleton<SimulationRunner>();
            });

            if (!result)
            {
                return Results.BadRequest("Initialization failed");
            }

            var stations = engineManager.GetEngineService<List<Station>>();
            var initData = InitEngineDataBuilder.BuildInitEngineData(stations);

            var envelope = new Envelope { InitEngineData = initData };
            var data = envelope.ToByteArray();

            return Results.File(data, "application/octet-stream");
        });

        app.MapPatch("/update-weights/{costId}", (
            EngineManager.EngineManager engineManager,
            int costId,
            CostWeightDTO weight) =>
        {
            if (!engineManager.IsInitialized)
            {
                return Results.BadRequest("Engine not initialized");
            }

            if (weight.CostId != costId)
            {
                return Results.BadRequest("Route costId does not match body costId.");
            }

            var settings = engineManager.GetEngineService<Engine.Init.EngineSettings>();

            return Results.Ok();
        });

        app.MapPost("/simulation/stop", async (EngineManager.EngineManager engineManager) =>
        {
            if (!engineManager.IsInitialized)
                return Results.BadRequest("Engine not initialized");

            var simulationRunner = engineManager.GetEngineService<SimulationRunner>();

            if (!simulationRunner.IsRunning)
                return Results.BadRequest("Simulation is not running");

            await simulationRunner.StopAsync();

            return Results.Ok(new { message = "Simulation stopped successfully" });
        });

        app.MapPost("/simulation/pause", async (EngineManager.EngineManager engineManager) =>
        {
            if (!engineManager.IsInitialized)
                return Results.BadRequest("Engine not initialized");

            var simulationRunner = engineManager.GetEngineService<SimulationRunner>();

            if (!simulationRunner.IsRunning)
                return Results.BadRequest("Simulation is not running");

            if (simulationRunner.IsPaused)
                return Results.BadRequest("Simulation is already paused");

            await simulationRunner.PauseAsync();

            return Results.Ok(new { message = "Simulation paused successfully" });
        });

        app.MapPost("/simulation/unpause", async (EngineManager.EngineManager engineManager) =>
        {
            if (!engineManager.IsInitialized)
                return Results.BadRequest("Engine not initialized");

            var simulationRunner = engineManager.GetEngineService<SimulationRunner>();

            if (!simulationRunner.IsRunning)
                return Results.BadRequest("Simulation is not running");

            if (!simulationRunner.IsPaused)
                return Results.BadRequest("Simulation is not paused");

            await simulationRunner.ResumeAsync();

            return Results.Ok(new { message = "Simulation resumed successfully" });
        });

        app.Map("/ws/simulation", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var engineManager = context.RequestServices.GetRequiredService<EngineManager.EngineManager>();

            if (!engineManager.IsInitialized)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("Engine not initialized. Call /init-engine first.");
                return;
            }

            var webSocketService = engineManager.GetEngineService<SimulationWebSocketService>();
            var simulationRunner = engineManager.GetEngineService<SimulationRunner>();

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

            await simulationRunner.StartAsync(context.RequestAborted);

            try
            {
                await webSocketService.HandleConnectionAsync(webSocket, context.RequestAborted);
            }
            catch (Exception ex)
            {
                var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("WebSocket");
                logger.LogError(ex, "WebSocket error");
            }
            finally
            {
                await simulationRunner.StopAsync();
            }
        });

        app.Run();
    }
}
