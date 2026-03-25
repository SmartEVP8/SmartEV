namespace Testing;

using Core.Charging;
using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Engine.Events;
using Engine.Routing;
using Engine.Services;
using Engine.Vehicles;

public class ReservationRequestTests
{
    private const ushort _stationId = 1;
    private const int _evId = 0;
    private readonly StationService _stationService;
    private readonly EventScheduler _scheduler;
    private readonly EVStore _evStore;

    public ReservationRequestTests()
    {
        var station = MakeStation();

        _scheduler = new EventScheduler([]);
        _evStore = new EVStore(10);

        _stationService = new StationService(
            stations: [station],
            integrator: null!,
            scheduler: _scheduler,
            pathDeviator: new PathDeviator(new StubRouter()),
            random: new Random(42));

        var ev = new EV(
            battery: new Battery(capacity: 50, maxChargeRate: 20, stateOfCharge: 30f, socket: Socket.CCS2),
            efficiency: 2,
            preferences: new Preferences(priceSensitivity: 0.5f, minAcceptableCharge: 0.1f, maxPathDeviation: 1.0f),
            journey: new Journey(
                departure: new Time(0),
                originalDuration: new Time(1000),
                path: new Paths([new Position(0.0, 0.0), new Position(2.0, 2.0)])));
        _evStore.Set(_evId, ref ev);
    }

    private static EnergyPrices MakeEnergyPrices()
    {
        var lines = new List<string> { "Day,Hour,Price" };
        foreach (var day in Enum.GetValues<DayOfWeek>())
            for (var h = 0; h < 24; h++)
                lines.Add($"{day},{h},3.00");

        var path = Path.GetTempFileName();
        File.WriteAllLines(path, lines);
        return new EnergyPrices(new FileInfo(path));
    }

    private static Station MakeStation() => new(
        id: _stationId,
        name: "Test Station",
        address: "Test Address",
        position: new Position(1.0, 1.0),
        chargers: [],
        random: new Random(42),
        energyPrices: MakeEnergyPrices());

    private class StubRouter : IDestinationRouter
    {
        public (float duration, string polyline) QueryDestination(double[] coords)
            => (700, "_p~iF~ps|U_ulLnnqC");
    }

    [Fact]
    public void SetsReservationAndSchedulesArrival()
    {
        _stationService.HandleReservationRequest(
            new ReservationRequest(_evId, _stationId, new Time(0)), _evStore);

        Assert.Equal(_stationId, _evStore.Get(_evId).HasReservationAtStationId);
        Assert.IsType<ArriveAtStation>(_scheduler.GetNextEvent());
    }

    [Fact]
    public void ExistingReservation_CancelsOldArrival()
    {
        _stationService.HandleReservationRequest(
            new ReservationRequest(_evId, _stationId, new Time(0)), _evStore);

        _stationService.HandleReservationRequest(
            new ReservationRequest(_evId, _stationId, new Time(0)), _evStore);

        Assert.IsType<ArriveAtStation>(_scheduler.GetNextEvent());
        Assert.Null(_scheduler.GetNextEvent());
    }
}