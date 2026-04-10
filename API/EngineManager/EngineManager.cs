namespace API.EngineManager;

using Engine.Init;

/// <summary>
/// Manages the lifecycle and configuration of the simulation engine.
/// Allows runtime (re)initialization of engine services with custom configuration,
/// by building a dedicated service provider for engine-scoped dependencies.
///
/// Note: This manager creates its own internal service provider for engine services.
/// It does NOT integrate engine-scoped services into the main API DI container.
/// To access engine services elsewhere, resolve them via EngineManager.GetEngineService<T>().
/// </summary>
public class EngineManager
{
    private readonly IServiceCollection _engineServices;
    private IServiceProvider _engineProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EngineManager"/> class.
    /// </summary>
    public EngineManager() => _engineServices = new ServiceCollection();

    /// <summary>
    /// Initializes or re-initializes the engine with the provided configuration.
    /// Rebuilds the engine's internal service provider and dependencies.
    /// </summary>
    /// <param name="configDTO">The engine configuration DTO.</param>
    /// <returns>True if initialization succeeded.</returns>
    public async Task<bool> InitializeAsync(
        EngineInitConfigDTO configDTO,
        Action<IServiceCollection>? configureServices = null)
    {
        try
        {
            var settings = EngineConfiguration.CreateDefaultSettings();
            var mergedSettings = MergeSettings(configDTO, settings);

            _engineServices.Clear();
            _engineServices.AddSingleton(mergedSettings);
            Init.InitEngine(_engineServices);

            configureServices?.Invoke(_engineServices);
            _engineProvider = _engineServices.BuildServiceProvider();
            return true;
        }
        catch
        {
            return false;
        }
    }



    private EngineSettings MergeSettings(EngineInitConfigDTO configDTO, EngineSettings defaultSettings)
    {
        return defaultSettings with
        {
            CurrentAmoutOfEVsInDenmark = configDTO.MaximumEVs,
            Seed = new Random(configDTO.Seed),
            StationFactoryOptions = defaultSettings.StationFactoryOptions with
            {
                DualChargingPointProbability = configDTO.DualChargerProbability,
                TotalChargers = configDTO.NumberOfChargers
            },
            CostConfig = configDTO.CostWeights.ToDomain(defaultSettings.CostConfig),
        };
    }

    /// <summary>
    /// Resolves an engine-scoped service from the engine's internal service provider.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The resolved service instance.</returns>
    public T GetEngineService<T>()
        where T : notnull
        => _engineProvider.GetRequiredService<T>();
}
