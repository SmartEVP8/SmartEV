namespace API;

using Services;
using Engine.Events;
using Engine.Init;
using Engine.Services;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // The Engine registers many heavyweight singletons (OSRM, stations, grids, etc.).
        // In Development, the default host settings validate the service graph on build,
        // which can force those singletons to instantiate before Kestrel starts.
        // That makes the API appear to "hang" and never bind to the port.
        if (builder.Environment.IsDevelopment())
        {
            builder.Host.UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = false;
            });
        }

        // Add services to the container
        builder.Services.AddLogging();
        builder.Services.AddSingleton(EngineConfiguration.CreateDefaultSettings());

        builder.Services.AddSingleton<SimulationChannel>();
        builder.Services.AddSingleton<SimulationEngineService>();
        builder.Services.AddSingleton<IEngineEventSubscriber>(sp => sp.GetRequiredService<SimulationEngineService>());

        Init.InitEngine(builder.Services);

        builder.Services.AddSingleton<SimulationStateService>();
        builder.Services.AddSingleton<SimulationMessageHandler>();
        builder.Services.AddSingleton<EnvelopeWebSocketHandler>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulationEngineService>());

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseWebSockets();

        // Map WebSocket endpoint
        app.MapSimulationWebSocket();

        app.Run();
    }
}
