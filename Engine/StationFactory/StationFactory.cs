namespace Engine.StationFactory;

using System.Text.Json;
using Core.Charging;
using Core.Shared;

/// <summary>
/// Factory for creating stations from a JSON file containing charging location data.
/// Socket distribution is based on predefined dataset counts rather than random selection.
/// Generation is deterministic for a given seed.
/// </summary>
public class StationFactory
{
    private readonly StationFactoryOptions _options;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the StationFactory class with the specified options.
    /// </summary>
    /// <param name="options">
    /// The options for configuring the station factory.
    /// </param>
    /// <remarks>
    /// The seed in the options ensures that the same stations are generated for the same input file and seed.
    /// </remarks>
    public StationFactory(StationFactoryOptions options)
    {
        _options = options;
        _random = new Random(_options.Seed);
    }

    /// <summary>
    /// Creates a list of stations by reading charging location data from a JSON file.
    /// The generated chargers are distributed to match the dataset socket counts.
    /// For the same input file and seed, the generated stations will always be identical.
    /// </summary>
    /// <param name="filePath">
    /// Path to the JSON file containing charging location data.
    /// </param>
    /// <returns>
    /// A list of created stations.
    /// </returns>
    public List<Station> CreateStations(string filePath)
    {
        var json = File.ReadAllText(filePath);

        var locations = JsonSerializer.Deserialize<List<ChargingLocationDTO>>(json)
            ?? new List<ChargingLocationDTO>();

        if (locations.Count == 0)
        {
            return new List<Station>();
        }

        var socketPool = CreateSocketPool();
        Shuffle(socketPool);

        var chargerCountsPerStation = DistributeChargersAcrossStations(locations.Count, socketPool.Count);

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
    /// Creates either a single or dual charging point using the same socket type on both sides.
    /// </summary>
    /// <param name="socket">
    /// The socket type to use for the charging point.
    /// </param>
    /// <returns>
    /// The created charging point.
    /// </returns>
    private IChargingPoint CreateChargingPoint(Socket socket)
    {
        bool isDual = _options.UseDualChargingPoints &&
                      _random.NextDouble() < _options.DualChargingPointProbability;

        var connectors = new List<Connector> { new Connector(socket) };

        if (!isDual)
        {
            return new SingleChargingPoint(connectors);
        }

        var mirroredConnectors = new List<Connector> { new Connector(socket) };
        return new DualChargingPoint(connectors, mirroredConnectors);
    }

    /// <summary>
    /// Creates the full socket pool based on dataset counts.
    /// </summary>
    /// <returns>
    /// A list containing the full socket distribution.
    /// </returns>
    private static List<Socket> CreateSocketPool()
    {
        var pool = new List<Socket>();

        AddSockets(pool, Socket.CHADEMO, 167);
        AddSockets(pool, Socket.Type2SocketOnly, 4514);
        AddSockets(pool, Socket.NACS, 14);
        AddSockets(pool, Socket.CCS, 1472);
        AddSockets(pool, Socket.Type2Tethered, 1044);

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
