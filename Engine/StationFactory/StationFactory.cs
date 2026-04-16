using Serilog;
namespace Engine.StationFactory;

using System.Text.Json;
using Core.Charging;
using Core.Shared;

/// <summary>
/// Factory for creating stations from a JSON file containing charging location data.
/// Socket distribution is based on configurable socket probabilities.
/// Generation is deterministic for a given seed.
/// </summary>
public class StationFactory
{
    private readonly StationFactoryOptions _options;
    private readonly Random _random;
    private readonly EnergyPrices _energyPrices;
    private readonly FileInfo _stationsFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="StationFactory"/> class with the specified options and random seed.
    /// </summary>
    /// <param name="options">The configuration options for station generation.</param>
    /// <param name="random">The seed for random number generation to ensure deterministic output.</param>
    /// <param name="energyPrices">Dynamic energy prices based on time of day.</param>
    /// <param name="stationsFile">The file containing the station location data.</param>
    /// <exception cref="ArgumentNullException">Thrown if options is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if TotalChargers is not greater than zero, or if probabilities are not between 0 and 1.</exception>
    /// <exception cref="ArgumentException">Thrown if SocketProbabilities is empty, contains negative probabilities, or does not sum to approximately 1.</exception>
    /// <exception cref="InvalidOperationException">Thrown if there are not enough chargers to assign at least one to each station.</exception>
    public StationFactory(StationFactoryOptions options, Random random, EnergyPrices energyPrices, FileInfo stationsFile)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TotalChargers <= 0)
        {
            throw LogHelper.Error(0, 0, new ArgumentOutOfRangeException(
                nameof(options.TotalChargers),
                "TotalChargers must be greater than zero."),
                ("TotalChargers", options.TotalChargers));
        }

        if (options.DualChargingPointProbability < 0 || options.DualChargingPointProbability > 1)
        {
            throw LogHelper.Error(0, 0, new ArgumentOutOfRangeException(
                nameof(options.DualChargingPointProbability),
                "DualChargingPointProbability must be between 0 and 1."));
        }

        _options = options;
        _random = random;
        _energyPrices = energyPrices;
        _stationsFile = stationsFile ?? throw LogHelper.Error(0, 0, new ArgumentNullException(nameof(stationsFile), "Stations file cannot be null."));
    }

    /// <summary>
    /// Creates a list of stations based on the provided JSON file containing stations.
    /// Each station is assigned a number of chargers based on the total chargers and the number of stations, ensuring at least one charger per station.
    /// Chargers are created with socket types distributed according to the specified probabilities.
    /// The order of socket assignment is randomised to avoid clustering of connector types at the first stations.
    /// Throws exceptions if the file does not exist or if there are not enough chargers to assign at least one to each station.
    /// </summary>
    /// <returns> Returns a list of created stations. </returns>
    public List<Station> CreateStations()
    {
        var json = File.ReadAllText(_stationsFile.FullName);
        var locations = JsonSerializer.Deserialize<List<StationLocationDTO>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw LogHelper.Error(0, 0, new InvalidOperationException("JSON file was empty or null."));

        if (locations.Count == 0)
            throw LogHelper.Error(0, 0, new InvalidOperationException("Station locations JSON file was empty."));

        var chargerCountsPerStation = DistributeChargersAcrossStations(locations.Count, _options.TotalChargers) ?? throw LogHelper.Error(0, 0, new InvalidOperationException("Failed to distribute chargers across stations."));

        var stations = new List<Station>(locations.Count);
        ushort nextStationId = 0;
        var chargerId = 1;

        for (var i = 0; i < locations.Count; i++)
        {
            var chargerCount = chargerCountsPerStation[i];
            var chargers = new List<ChargerBase>(chargerCount);

            for (var j = 0; j < chargerCount; j++)
            {
                chargers.Add(CreateCharger(chargerId++));
            }

            stations.Add(CreateStation(nextStationId++, locations[i], chargers));
        }

        Log.Information($"Created {stations.Count} stations with a total of {chargerId - 1} chargers.");
        return stations;
    }

    /// <summary>
    /// Creates a station from a charging location DTO and a predefined charger list.
    /// </summary>
    /// <param name="id">The station identifier.</param>
    /// <param name="location">The charging location data.</param>
    /// <param name="chargers">The chargers assigned to the station.</param>
    /// <returns>The created station.</returns>
    private Station CreateStation(ushort id, StationLocationDTO location, List<ChargerBase> chargers)
    {
        var position = new Position(location.Longitude, location.Latitude);

        var station = new Station(
            id,
            location.Address ?? string.Empty,
            position,
            chargers,
            _energyPrices);

        return station;
    }

    /// <summary>
    /// Creates a charger.
    /// </summary>
    /// <param name="chargerId">
    /// The charger identifier within the station.
    /// </param>
    /// <returns>
    /// The created charger.
    /// </returns>
    private ChargerBase CreateCharger(int chargerId)
    {
        var connectors = CreateConnectorSet();

        if (ShouldCreateDualChargingPoint())
        {
            return new DualCharger(chargerId, _options.MaxPowerKW, connectors);
        }

        return new SingleCharger(chargerId, _options.MaxPowerKW, connectors);
    }

    private Connectors CreateConnectorSet()
        => new((new Connector(_options.MaxPowerKW), new Connector(_options.MaxPowerKW)));

    private bool ShouldCreateDualChargingPoint()
        => _random.NextDouble() < _options.DualChargingPointProbability;

    /// <summary>
    /// Distributes the total number of chargers across the available stations.
    /// Ensures every station gets at least one charger.
    /// The distribution is deterministic for a given seed.
    /// </summary>
    /// <param name="stationCount">The number of stations.</param>
    /// <param name="totalChargers">The total number of chargers to distribute.</param>
    /// <returns>A list where each element is the number of chargers assigned to the corresponding station.</returns>
    private List<int> DistributeChargersAcrossStations(int stationCount, int totalChargers)
    {
        if (stationCount <= 0)
            throw LogHelper.Error(0, 0, new ArgumentException("Station count must be greater than zero."), ("StationCount", stationCount));

        if (totalChargers < stationCount)
            throw LogHelper.Error(0, 0, new InvalidOperationException("Not enough chargers to give at least one to each station."), ("StationCount", stationCount), ("TotalChargers", totalChargers));

        var result = Enumerable.Repeat(1, stationCount).ToList();
        var remaining = totalChargers - stationCount;

        while (remaining > 0)
        {
            result[_random.Next(stationCount)]++;
            remaining--;
        }

        return result;
    }
}
