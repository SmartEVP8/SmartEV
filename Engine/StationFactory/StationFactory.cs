namespace Engine.StationFactory;

using System.Text.Json;
using Core.Charging;
using Core.Shared;

public class StationFactory
{
    private readonly Random _random;

    public StationFactory(Random random)
    {
        _random = random;
    }

    public List<Station> CreateStations(string filePath)
    {
        var json = File.ReadAllText(filePath);

        var locations = JsonSerializer.Deserialize<List<ChargingLocationDTO>>(json)
                        ?? new List<ChargingLocationDTO>();

        var stations = new List<Station>();
        ushort nextId = 1;

        foreach (var location in locations)
        {
            stations.Add(CreateStation(nextId++, location));
        }

        return stations;
    }

    private Station CreateStation(ushort id, ChargingLocationDTO location)
{
    int chargerCount = _random.Next(2, 9);

    var chargers = new List<Charger>(chargerCount);

    for (int i = 0; i < chargerCount; i++)
    {
        chargers.Add(CreateCharger());
    }

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

    private Charger CreateCharger()
    {
        var connector = new Connector(Socket.Type2SocketOnly);
        return new Charger(connector);
    }
}