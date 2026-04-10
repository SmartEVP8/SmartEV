namespace Core.test.Builders;

using Core.Charging;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using System.Collections.Immutable;

public static class CoreTestData
{
    public static readonly EnergyPrices EnergyPrices =
          new(new FileInfo(AppContext.GetData("EnergyPricesPath") as string
              ?? throw new InvalidDataException("EnergyPricesPath not set.")), new Random(1));

    public static Station Station(
        ushort id,
        Position? pos = null,
        EnergyPrices? energyPrices = null,
        int? queueSize = null,
        List<ChargerBase>? chargers = null)
    {
        var position = pos ?? new Position(0, 0);
        var prices = energyPrices ?? EnergyPrices;

        List<ChargerBase> chargerList;
        if (chargers != null)
            chargerList = chargers;
        else if (queueSize.HasValue)
            chargerList = [CreateFakeChargerWithQueue(queueSize.Value)];
        else
            chargerList = [CreateFakeChargerWithQueue(0)]; // Default: one empty charger

        return new Station(id, string.Empty, string.Empty, position, chargerList, prices);
    }

    public static Dictionary<ushort, Station> Stations(params (ushort Id, double Lon, double Lat)[] stations)
        => stations.ToDictionary(s => s.Id, s => Station(s.Id, new Position(s.Lon, s.Lat)));

    public static Journey Journey(List<Position>? waypoints, Time departure = default, Time originalDuration = default, float distanceMeter = 100)
    {
        if (waypoints == null)
        {
            return new(departure, originalDuration, distanceMeter, new List<Position>([new(0, 0), new(1, 1)]));
        }

        return new(departure, originalDuration, distanceMeter, [.. waypoints]);
    }

    public static Battery Battery(
        ushort capacity = 100,
        ushort maxChargeRate = 150,
        float stateOfCharge = 0.2f,
        Socket socket = Socket.CCS2) => new(capacity, maxChargeRate, stateOfCharge, socket);

    public static Preferences Preferences(
        float PriceSensitivity = 1f,
        float MinAcceptableCharge = 0.1f,
        float MaxPathDeviation = 10.0f) => new(PriceSensitivity, MinAcceptableCharge, MaxPathDeviation);

    public static EV EV(
        List<Position>? waypoints = null,
        Battery? battery = null,
        Preferences? preferences = null,
        ushort efficiency = 150,
        uint originalDuration = 100000u,
        Time departureTime = default,
        float distanceMeter = 100)
    {
        return new(
            battery ?? Battery(),
            preferences ?? Preferences(),
            Journey(waypoints, originalDuration: originalDuration, departure: departureTime, distanceMeter: distanceMeter),
            efficiency);
    }

    internal sealed class FixedEnergyPrices(float fixedPrice) : EnergyPrices(new FileInfo("data/energy_prices.csv"), new Random(42))
    {
        private readonly float _fixedPrice = fixedPrice;

        public float CalculatePrice() => _fixedPrice;
    }

    private sealed class FakeCharger() : ChargerBase(id: 1, maxPowerKW: 100)
    {
        public override ImmutableArray<Socket> GetSockets() => [Socket.CCS2];
    }

    public static SingleCharger SingleCharger(int id, int maxPowerKW = 150)
    {
        var connectors = new Connectors([new Connector(Socket.CCS2)]);
        var point = new SingleChargingPoint(connectors);
        return new SingleCharger(id, maxPowerKW, point);
    }

    public static DualCharger DualCharger(int id, int maxPowerKW = 150)
    {
        var point = new DualChargingPoint(new Connectors([new Connector(Socket.CCS2)]));
        return new DualCharger(id, maxPowerKW, point);
    }

    private static ChargerBase CreateFakeChargerWithQueue(int queueSize)
    {
        var charger = new FakeCharger();
        for (var i = 0; i < queueSize; i++)
        {
            charger.Queue.Enqueue(i);
        }

        return charger;
    }
}
