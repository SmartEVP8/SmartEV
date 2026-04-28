namespace Headless;

using Engine;
using Engine.Cost;
using Engine.Events;
using Engine.Grid;
using Engine.Init;
using Engine.Metrics;
using Engine.Routing;
using Engine.Spawning;
using Engine.Services;
using Engine.Vehicles;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Templates;

/// <summary>
/// The entry point for the headless execution of the Engine. Initializes all necessary services and starts the simulation.
/// </summary>
public static class Program
{
    /// <summary>
    /// The main method initializes the Engine with the required services and configurations, then starts the simulation by resolving necessary services from the dependency injection.
    /// </summary>
    /// <returns>The running simulation.</returns>
    public static async Task Main()
    {
        var services = new ServiceCollection();
        var settings = EngineConfiguration.CreateDefaultSettings() ?? throw new InvalidOperationException("Failed to create default engine settings. This should not happen.");

        services.AddSingleton(settings);
        Init.InitEngine(services);
        var provider = services.BuildServiceProvider() ?? throw new InvalidOperationException("Failed to build service provider. This should not happen.");
        provider.GetRequiredService<EventScheduler>();
        provider.GetRequiredService<IOSRMRouter>();
        provider.GetRequiredService<ICostStore>();
        provider.GetRequiredService<MetricsService>();
        provider.GetRequiredService<EVFactory>();
        provider.GetRequiredService<SpatialGrid>();
        provider.GetRequiredService<IJourneySamplerProvider>();
        provider.GetRequiredService<StationService>();

        var coordinator = provider.GetRequiredService<Simulation>() ?? throw new InvalidOperationException("Failed to resolve Simulation from service provider. This should not happen.");
        var formatter = new ExpressionTemplate(
            "[{@l:u3}] {@m}\n{@x}");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                formatter,
                "logs/Headless-verbose-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm") + ".jsonl",
                restrictedToMinimumLevel: LogEventLevel.Verbose)
            .WriteTo.File(
                formatter,
                "logs/Headless-information-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm") + ".jsonl",
                restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                formatter,
                "logs/Headless-warning-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm") + ".jsonl",
                restrictedToMinimumLevel: LogEventLevel.Warning)
            .WriteTo.File(
                formatter,
                "logs/Headless-error-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm") + ".jsonl",
                restrictedToMinimumLevel: LogEventLevel.Error)
            .CreateLogger();
        await coordinator.Run();
    }
}
