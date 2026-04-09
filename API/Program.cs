namespace API;

using Services;
using Engine.Events;
using Engine.Services;
using Engine.Cost;
using API.EngineManager;
using Protocol;
using Google.Protobuf;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<EngineManager.EngineManager>();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins("http://localhost:5173")
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseWebSockets();
        app.UseCors("AllowFrontend");

        app.MapSimulationWebSocket();

        app.MapGet("/weights", async () =>
        {
            var weights = CostWeightMetadata.All;
            return Results.Ok(weights);
        });

        app.MapPost("/init-engine", async (EngineManager.EngineManager engineManager, EngineInitConfigDTO config) =>
        {
            var result = await engineManager.InitializeAsync(config, services =>
            {
                services.AddLogging();
                services.AddSingleton<SimulationChannel>();
                services.AddSingleton<SimulationEngineService>();
                services.AddSingleton<IEngineEventSubscriber>(sp => sp.GetRequiredService<SimulationEngineService>());
                services.AddSingleton<SimulationMessageHandler>();
                services.AddSingleton<EnvelopeWebSocketHandler>();
            });

            var stationService = engineManager.GetEngineService<StationService>();
            var initData = new InitEngineData();

            try
            {
                foreach (var station in stationService.GetAllStations())
                {
                    var stationInit = new StationInit
                    {
                        Id = station.Id,
                        Address = station.Address,
                        Pos = new Position { Lat = station.Position.Latitude, Lon = station.Position.Longitude },
                    };
                    initData.Stations.Add(stationInit);

                    foreach (var charger in station.Chargers)
                    {
                        var chargerProto = new Charger
                        {
                            Id = charger.Id,
                            MaxPowerKw = charger.MaxPowerKW,
                            StationId = station.Id,
                            IsDual = charger.GetSockets().Length > 1,
                        };
                        initData.Chargers.Add(chargerProto);
                    }
                }
            }
            catch
            {
            }

            var envelope = new Envelope { InitEngineData = initData };
            var data = envelope.ToByteArray();

            return result
                ? Results.File(data, "application/octet-stream")
                : Results.BadRequest("Initialization failed");
        });

        await app.RunAsync();
    }
}
