using Serilog.Events;
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
        var settings = EngineConfiguration.CreateDefaultSettings();

        services.AddSingleton(settings);
        Init.InitEngine(services);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<EventScheduler>();
        provider.GetRequiredService<IOSRMRouter>();
        provider.GetRequiredService<ICostStore>();
        provider.GetRequiredService<MetricsService>();
        provider.GetRequiredService<EVFactory>();
        provider.GetRequiredService<SpatialGrid>();
        provider.GetRequiredService<IJourneySamplerProvider>();
        provider.GetRequiredService<StationService>();

        var coordinator = provider.GetRequiredService<Simulation>();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                "logs/Headless-verbose-.txt",
                restrictedToMinimumLevel: LogEventLevel.Verbose,
                outputTemplate: "{Level:u3}: {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day)
            .WriteTo.File(
                "logs/Headless-information-.txt",
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Level:u3}: {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day)
            .WriteTo.File(
                "logs/Headless-warning-.txt",
                restrictedToMinimumLevel: LogEventLevel.Warning,
                outputTemplate: "{Level:u3}: {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day)
            .CreateLogger();
        await coordinator.Run();
    }
}
