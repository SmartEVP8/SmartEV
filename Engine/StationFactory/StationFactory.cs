namespace Engine.StationFactory;

using System.Text.Json;
using Core.Charging;
using Core.Charging.ChargingModel.Chargepoint;
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
            throw new ArgumentOutOfRangeException(
                nameof(options.TotalChargers),
                "TotalChargers must be greater than zero.");
        }

        if (options.DualChargingPointProbability < 0 || options.DualChargingPointProbability > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.DualChargingPointProbability),
                "DualChargingPointProbability must be between 0 and 1.");
        }

        if (options.SocketProbabilities.Count == 0)
        {
            throw new ArgumentException(
                "SocketProbabilities must contain at least one socket type.", nameof(options));
        }

        if (options.SocketProbabilities.Values.Any(p => p < 0))
        {
            throw new ArgumentException(
                "Socket probabilities cannot be negative.", nameof(options));
        }

        var totalProbability = options.SocketProbabilities.Values.Sum();
        if (Math.Abs(totalProbability - 1.0) > 0.01)
        {
            throw new ArgumentException(
                "Socket probabilities must sum approximately to 1.0.", nameof(options));
        }

        if (options.MultiSocketChargerProbability < 0 || options.MultiSocketChargerProbability > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MultiSocketChargerProbability),
                "MultiSocketChargerProbability must be between 0 and 1.");
        }

        if (stationsFile == null)
        {
            throw new ArgumentNullException(nameof(stationsFile), "Stations file cannot be null.");
        }

        _options = options;
        _random = random;
        _energyPrices = energyPrices;
        _stationsFile = stationsFile;
    }

    /// <summary>
    /// Creates a list of stations based on the provided JSON file containing stations.
    /// Each station is assigned a number of chargers based on the total chargers and the number of stations, ensuring at least one charger per station.
    /// Chargers are created with socket types distributed according to the specified probabilities.
    /// The order of socket assignment is randomised to avoid clustering of connector types at the first stations.
    /// Throws exceptions if the file does not exist or if there are not enough chargers to assign at least one to each station.
    /// </summary>
    /// <returns> Returns a list of created stations. </returns>
    public Dictionary<ushort, Station> CreateStations()
    {
        var json = File.ReadAllText(_stationsFile.FullName);
        var locations = JsonSerializer.Deserialize<List<StationLocationDTO>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("JSON file was empty or null.");

        if (locations.Count == 0)
            throw new InvalidOperationException("Station locations JSON file was empty.");

        var socketPool = CreateSocketPool();
        Shuffle(socketPool);

        var chargerCountsPerStation = DistributeChargersAcrossStations(locations.Count, _options.TotalChargers);

        var stations = new Dictionary<ushort, Station>(locations.Count);
        ushort nextStationId = 1;
        var socketIndex = 0;

        for (var i = 0; i < locations.Count; i++)
        {
            var chargerCount = chargerCountsPerStation[i];
            var chargers = new List<ChargerBase>(chargerCount);

            for (var chargerId = 1; chargerId <= chargerCount; chargerId++)
            {
                var socket = socketPool[socketIndex++];
                chargers.Add(CreateCharger(chargerId, socket));
            }

            stations.Add(nextStationId, CreateStation(nextStationId++, locations[i], chargers));
        }

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
            location.Name ?? string.Empty,
            location.Address ?? string.Empty,
            position,
            chargers,
            _random,
            _energyPrices);

        station.CalculatePrice(DateTime.Now.DayOfWeek, DateTime.Now.Hour);

        return station;
    }

    /// <summary>
    /// Creates a charger using a predefined socket type.
    /// </summary>
    /// <param name="chargerId">
    /// The charger identifier within the station.
    /// </param>
    /// <param name="socket">
    /// The socket type assigned to the charger.
    /// </param>
    /// <returns>
    /// The created charger.
    /// </returns>
    private ChargerBase CreateCharger(int chargerId, Socket socket)
    {
        int maxPowerKW = socket.PowerKW();
        return CreateChargingPoint(socket) switch
        {
            SingleChargingPoint s => new SingleCharger(chargerId, maxPowerKW, s),
            DualChargingPoint d => new DualCharger(chargerId, maxPowerKW, d),
            var p => throw new InvalidOperationException($"Unknown charging point type: {p.GetType()}")
        };
    }

    /// <summary>
    /// Creates either a single or dual charging point.
    /// A dual point takes one Connectors set — the right side is always a copy of the left.
    /// </summary>
    private IChargingPoint CreateChargingPoint(Socket primarySocket)
    {
        var connectors = CreateConnectorSet(primarySocket);

        if (!ShouldCreateDualChargingPoint())
            return new SingleChargingPoint(connectors);

        return new DualChargingPoint(connectors);
    }

    private Connectors CreateConnectorSet(Socket primarySocket)
    {
        var connectors = new List<Connector> { new(primarySocket) };

        if (!ShouldCreateMultiSocketCharger())
            return new Connectors(connectors);

        var availableSockets = _options.SocketProbabilities.Keys
            .Where(socket => socket != primarySocket)
            .ToList();

        if (availableSockets.Count == 0)
            return new Connectors(connectors);

        var secondarySocket = availableSockets[_random.Next(availableSockets.Count)];
        connectors.Add(new Connector(secondarySocket));

        return new Connectors(connectors);
    }

    private bool ShouldCreateDualChargingPoint()
        => _options.UseDualChargingPoints &&
           _random.NextDouble() < _options.DualChargingPointProbability;

    private bool ShouldCreateMultiSocketCharger()
        => _options.AllowMultiSocketChargers &&
           _random.NextDouble() < _options.MultiSocketChargerProbability;

    private List<Socket> CreateSocketPool()
    {
        var pool = new List<Socket>(_options.TotalChargers);
        var probabilities = _options.SocketProbabilities.ToList();
        var assignedCount = 0;

        for (var i = 0; i < probabilities.Count; i++)
        {
            var (socket, probability) = probabilities[i];
            int count;

            if (i == probabilities.Count - 1)
            {
                count = _options.TotalChargers - assignedCount;
            }
            else
            {
                count = (int)Math.Round(probability * _options.TotalChargers);
                assignedCount += count;
            }

            for (var j = 0; j < count; j++)
                pool.Add(socket);
        }

        return pool;
    }

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
            throw new ArgumentException("Station count must be greater than zero.");

        if (totalChargers < stationCount)
            throw new InvalidOperationException("Not enough chargers to give at least one to each station.");

        var result = Enumerable.Repeat(1, stationCount).ToList();
        var remaining = totalChargers - stationCount;

        while (remaining > 0)
        {
            result[_random.Next(stationCount)]++;
            remaining--;
        }

        return result;
    }

    /// <summary>
    /// Shuffles a list in place using the Fisher-Yates algorithm.
    /// The shuffle is deterministic for a given seed.
    /// </summary>
    /// <remarks>
    /// This method is used to randomise the order of the socket pool before chargers are
    /// distributed across stations. Without shuffling, chargers would be assigned in the
    /// same fixed order as defined in <see cref="CreateSocketPool"/>, which would lead to
    /// unrealistic clustering of connector types at the first stations.
    /// </remarks>
    /// <typeparam name="T">The type of item in the list.</typeparam>
    /// <param name="list">The list to shuffle.</param>
    private void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
