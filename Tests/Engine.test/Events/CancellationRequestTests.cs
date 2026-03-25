namespace Testing;

using Core.Charging;
using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Engine.Events;
using Engine.Routing;
using Engine.Services;
using Engine.Vehicles;

public class CancelRequestTests
{
    private class StubRouter : IDestinationRouter
    {
        public (float duration, string polyline) QueryDestination(double[] coords)
            => (700, "_p~iF~ps|U_ulLnnqC");
    }

    private const ushort _stationID = 1;
    private const int _evID = 0;

    private readonly EventScheduler _scheduler = new([]);
    private readonly EVStore _evStore = new(10);
    private readonly StationService _stationService;

    public CancelRequestTests()
    {
        var station = MakeStation();

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
        _evStore.Set(_evID, ref ev);
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
        id: _stationID,
        name: "Test Station",
        address: "Test Address",
        position: new Position(1.0, 1.0),
        chargers: [],
        random: new Random(42),
        energyPrices: MakeEnergyPrices());

    [Fact]
    public void CancelsPendingArrivalEvent()
    {
        // First, make a reservation
        _stationService.HandleReservationRequest(
            new ReservationRequest(_evID, _stationID, new Time(0)), _evStore);

        // Get a reference to the EV to pass to the cancel method
        ref var ev = ref _evStore.Get(_evID);

        // Cancel the reservation
        _stationService.HandleCancelRequest(
            new ReservationRequest(_evID, _stationID, new Time(0)), ref ev);

        // Assert that the scheduled arrival event was cancelled
        Assert.Null(_scheduler.GetNextEvent());

        // Also assert that the EV no longer has a reservation
        Assert.Null(ev.HasReservationAtStationId);
    }
}