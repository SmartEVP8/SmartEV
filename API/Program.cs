using API;
using API.Services;
using Engine.Cost;
using Engine.Events;
using Engine.Grid;
using Engine.Init;
using Engine.Metrics;
using Engine.Routing;
using Engine.Spawning;
using Engine.Services;
using Engine.StationFactory;
using Engine.Vehicles;

var builder = WebApplication.CreateBuilder(args);
var dataPath = new DirectoryInfo("data/");

// Add services to the container
builder.Services.AddLogging();
builder.Services.AddSingleton(new EngineSettings
{
    CostConfig = new CostWeights
    {
        EffectiveQueueSize = 0.5f,
        PathDeviation = 10,
        PriceSensitivity = 10,
        AvailableChargerRatio = 1,
        ExpectedWaitTime = 1,
        Urgency = 1,
    },
    RunId = Guid.NewGuid(),
    MetricsConfig = new MetricsConfig
    {
        BufferSize = 5000,
        OutputDirectory = new DirectoryInfo("Perkuet"),
        RecordCarSnapshots = true,
        RecordArrivals = true,
        RecordStationSnapshots = true,
    },
    Seed = new Random(42),
    StationFactoryOptions = new StationFactoryOptions
    {
        UseDualChargingPoints = true,
        AllowMultiSocketChargers = true,
        DualChargingPointProbability = 0.5,
        MultiSocketChargerProbability = 1,
        TotalChargers = 10000,
    },
    IntervalToUpdateEVs = 5 * 60,
    BatteryIntervalForCheckUrgency = 0.05f,
    CurrentAmoutOfEVsInDenmark = 583320,
    ChargingStepSeconds = 60,
    SimulationEndTime = 10000 * 60,
    SnapshotInterval = 1000 * 60,
    EVDistributionWindowsSize = 1 * 60,
    EVSpawnFraction = 0.10f,
    EnergyPricesPath = new FileInfo(Path.Combine(dataPath.FullName, "energy_prices.csv")),
    OsrmPath = new FileInfo(Path.Combine(dataPath.FullName, "osrm/output.osrm")),
    CitiesPath = new FileInfo(Path.Combine(dataPath.FullName, "CityInfo.csv")),
    GridPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
    StationsPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
    PolygonPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark.polygon.json")),
});

builder.Services.AddSingleton<SimulationChannel>();
builder.Services.AddSingleton<SimulationEngineService>();
builder.Services.AddSingleton<IEngineEventSubscriber>(sp => sp.GetRequiredService<SimulationEngineService>());

Init.InitEngine(builder.Services);

builder.Services.AddSingleton<ISimulationStateService, SimulationStateService>();
builder.Services.AddSingleton<EnvelopeWebSocketHandler>();
builder.Services.AddSingleton<IEnvelopeMessageHandler, SimulationMessageHandler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulationEngineService>());

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseWebSockets();

// Map WebSocket endpoint
app.MapSimulationWebSocket();

app.Run();
