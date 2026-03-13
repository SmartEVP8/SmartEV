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

    /// <summary>
    /// Initializes a new instance of the StationFactory class with the specified options and random seed.
    /// </summary>
    /// <param name="options">
    /// The options for configuring the station factory.
    /// </param>
    /// <param name="seed">
    /// Seed used to initialise the random generator to ensure deterministic generation.
    /// </param>
    public StationFactory(StationFactoryOptions options, int seed)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TotalChargers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TotalChargers),
                "TotalChargers must be greater than zero.");
        }

        if (options.DualChargingPointProbability < 0 || options.DualChargingPointProbability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.DualChargingPointProbability),
                "DualChargingPointProbability must be between 0 and 1.");
        }

        if (options.SocketProbabilities.Count == 0)
        {
            throw new ArgumentException("SocketProbabilities must contain at least one socket type.",
                nameof(options));
        }

        if (options.SocketProbabilities.Values.Any(p => p < 0))
        {
            throw new ArgumentException("Socket probabilities cannot be negative.",
                nameof(options));
        }

        double totalProbability = options.SocketProbabilities.Values.Sum();
        if (Math.Abs(totalProbability - 1.0) > 0.01)
        {
            throw new ArgumentException("Socket probabilities must sum approximately to 1.0.",
                nameof(options));
        }

        if (options.MultiSocketChargerProbability < 0 || options.MultiSocketChargerProbability > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MultiSocketChargerProbability),
                "MultiSocketChargerProbability must be between 0 and 1.");
        }

        _options = options;
        _random = new Random(seed);
    }

    /// <summary>
    /// Creates a list of stations by reading charging location data from a JSON file.
    /// </summary>
    /// <param name="file">
    /// The JSON file containing charging location data.
    /// </param>
    /// <returns>
    /// A list of created stations.
    /// </returns>
    public List<Station> CreateStations(FileInfo file)
    {
        if (!file.Exists)
            throw new FileNotFoundException("Charging location file not found.", file.FullName);

        var json = File.ReadAllText(file.FullName);

        var locations = JsonSerializer.Deserialize<List<ChargingLocationDTO>>(json)
            ?? new List<ChargingLocationDTO>();

        if (locations.Count == 0)
        {
            return new List<Station>();
        }

        var socketPool = CreateSocketPool();
        Shuffle(socketPool);

        var chargerCountsPerStation = DistributeChargersAcrossStations(locations.Count, _options.TotalChargers);

        var stations = new List<Station>(locations.Count);
        ushort nextStationId = 1;
        int socketIndex = 0;

        for (int i = 0; i < locations.Count; i++)
        {
            int chargerCount = chargerCountsPerStation[i];
            var chargers = new List<Charger>(chargerCount);

            for (int chargerId = 1; chargerId <= chargerCount; chargerId++)
            {
                var socket = socketPool[socketIndex++];
                chargers.Add(CreateCharger(chargerId, socket));
            }

            stations.Add(CreateStation(nextStationId++, locations[i], chargers));
        }

        return stations;
    }

    /// <summary>
    /// Creates a station from a charging location DTO and a predefined charger list.
    /// </summary>
    /// <param name="id">
    /// The station identifier.
    /// </param>
    /// <param name="location">
    /// The charging location data.
    /// </param>
    /// <param name="chargers">
    /// The chargers assigned to the station.
    /// </param>
    /// <returns>
    /// The created station.
    /// </returns>
    private Station CreateStation(ushort id, ChargingLocationDTO location, List<Charger> chargers)
    {
        var position = new Position(location.Longitude, location.Latitude);
        var price = EnergyPrices.GetHourPrice(DayOfWeek.Monday, 12);

        return new Station(
            id,
            location.Name ?? string.Empty,
            location.Address ?? string.Empty,
            position,
            chargers,
            price,
            _random
        );
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
    private Charger CreateCharger(int chargerId, Socket socket)
    {
        int maxPowerKW = socket.PowerKW();
        IChargingPoint chargingPoint = CreateChargingPoint(socket);

        return new Charger(chargerId, maxPowerKW, chargingPoint);
    }

    /// <summary>
    /// Creates a charging point with one or two connectors based on the configured probabilities.
    /// The primary connector is determined by the provided socket type, while a secondary connector (if created) is randomly selected from the remaining socket types.
    /// </summary>
    /// <param name="primarySocket"></param>
    /// <returns></returns>
    private IChargingPoint CreateChargingPoint(Socket primarySocket)
    {
        var connectors = CreateConnectorSet(primarySocket);

        if (!ShouldCreateDualChargingPoint())
        {
            return new SingleChargingPoint(connectors);
        }

        return new DualChargingPoint(connectors, CopyConnectors(connectors));
    }

    /// <summary>
    /// Creates a set of connectors for a charging point based on the primary socket type and the configured probability for multi-socket chargers.
    /// The primary connector is always included, while a secondary connector is added based on the probabilities and available socket types.
    /// If multi-socket chargers are disabled or if there are no other socket types available, only the primary connector will be included.
    /// </summary>
    /// <param name="primarySocket"></param>
    /// <returns>
    /// A list of connectors for the charging point.
    /// </returns>
    private List<Connector> CreateConnectorSet(Socket primarySocket)
    {
        var connectors = new List<Connector> { new Connector(primarySocket) };

        if (!ShouldCreateMultiSocketCharger())
        {
            return connectors;
        }

        var availableSockets = _options.SocketProbabilities.Keys
            .Where(socket => socket != primarySocket)
            .ToList();

        if (availableSockets.Count == 0)
        {
            return connectors;
        }

        var secondarySocket = availableSockets[_random.Next(availableSockets.Count)];
        connectors.Add(new Connector(secondarySocket));

        return connectors;
    }

    /// <summary>
    /// Determines whether to create a dual charging point based on the configured options and probabilities.
    /// A dual charging point allows two vehicles to charge simultaneously at the same station, which can increase station utilization and reduce wait times for drivers.
    /// <summary>
    private bool ShouldCreateDualChargingPoint()
    {
        return _options.UseDualChargingPoints &&
            _random.NextDouble() < _options.DualChargingPointProbability;
    }

    /// <summary>
    /// Determines whether to create a multi-socket charger based on the configured options and probabilities.
    /// A multi-socket charger supports more than one socket type, which can increase the versatility
    /// </summary>
    /// <returns>
    /// True if a multi-socket charger should be created; otherwise, false.
    /// </returns>
    private bool ShouldCreateMultiSocketCharger()
    {
        return _options.AllowMultiSocketChargers &&
            _random.NextDouble() < _options.MultiSocketChargerProbability;
    }

    /// <summary>
    /// Creates a deep copy of a list of connectors to be used for the second charging point in a dual charging point configuration.
    /// This ensures that the two charging points have separate connector instances, allowing them to be used
    /// </summary>
    /// <param name="connectors"></param>
    /// <returns>
    /// A new list of connectors that are copies of the original connectors.
    /// </returns>
    private static List<Connector> CopyConnectors(IEnumerable<Connector> connectors)
    {
        return connectors
            .Select(connector => new Connector(connector.GetSocket()))
            .ToList();
    }

    /// <summary>
    /// Creates the full socket pool based on configured socket probabilities.
    /// The total number of sockets created matches <see cref="StationFactoryOptions.TotalChargers"/>.
    /// </summary>
    /// <returns>
    /// A list containing the generated socket distribution.
    /// </returns>
    private List<Socket> CreateSocketPool()
    {
        var pool = new List<Socket>(_options.TotalChargers);
        var probabilities = _options.SocketProbabilities.ToList();

        int assignedCount = 0;

        for (int i = 0; i < probabilities.Count; i++)
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

            AddSockets(pool, socket, count);
        }

        return pool;
    }

    /// <summary>
    /// Adds a given number of sockets of the same type to the pool.
    /// </summary>
    /// <param name="pool">
    /// The socket pool to add to.
    /// </param>
    /// <param name="socket">
    /// The socket type to add.
    /// </param>
    /// <param name="count">
    /// The number of sockets to add.
    /// </param>
    private static void AddSockets(List<Socket> pool, Socket socket, int count)
    {
        for (int i = 0; i < count; i++)
        {
            pool.Add(socket);
        }
    }

    /// <summary>
    /// Distributes the total number of chargers across the available stations.
    /// Ensures every station gets at least one charger.
    /// The distribution is deterministic for a given seed.
    /// </summary>
    /// <param name="stationCount">
    /// The number of stations.
    /// </param>
    /// <param name="totalChargers">
    /// The total number of chargers to distribute.
    /// </param>
    /// <returns>
    /// A list where each element is the number of chargers assigned to the corresponding station.
    /// </returns>
    private List<int> DistributeChargersAcrossStations(int stationCount, int totalChargers)
    {
        if (stationCount <= 0)
        {
            throw new ArgumentException("Station count must be greater than zero.");
        }

        if (totalChargers < stationCount)
        {
            throw new InvalidOperationException("Not enough chargers to give at least one to each station.");
        }

        var result = Enumerable.Repeat(1, stationCount).ToList();
        int remaining = totalChargers - stationCount;

        while (remaining > 0)
        {
            int stationIndex = _random.Next(stationCount);
            result[stationIndex]++;
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
    /// <typeparam name="T">
    /// The type of item in the list.
    /// </typeparam>
    /// <param name="list">
    /// The list to shuffle.
    /// </param>
    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
