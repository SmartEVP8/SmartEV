namespace Testing;

using Engine.Events;
using Core.Vehicles;
using Engine.Vehicles;
using Core.Shared;
using Engine.test.Builders;

public class CheckUrgencyHandlerTest
{
    private readonly EventScheduler _scheduler = new([]);
    private readonly EVStore _evStore = new(10);

    private EV MakeEV(float stateOfCharge) =>
        TestData.EV(
            waypoints: [new Position(10, 10), new Position(20, 20)],
            battery: TestData.Battery(capacity: 50, maxChargeRate: 20, stateOfCharge: stateOfCharge),
            preferences: TestData.Preferences(PriceSensitivity: 0.5f, MinAcceptableCharge: 0.0f),
            efficiency: 2,
            departureTime: 0);

    private CheckUrgencyHandler MakeHandler() =>
        new(_scheduler, _evStore, new Random(42));

    [Fact]
    public void LowUrgencySchedulesCheckUrgency()
    {
        var ev1 = MakeEV(stateOfCharge: 50f);
        _evStore.TryAllocate((_, ref ev) => ev = ev1, out var index1);

        MakeHandler().Handle(new CheckUrgency(index1, 1));

        Assert.Null(_scheduler.GetNextEvent());
    }

    [Fact]
    public void HighUrgencySchedulesFindCandidate()
    {
        var ev2 = MakeEV(stateOfCharge: 4f);
        _evStore.TryAllocate((_, ref ev) => ev = ev2, out var index1);

        MakeHandler().Handle(new CheckUrgency(index1, 1));

        Assert.IsType<FindCandidateStations>(_scheduler.GetNextEvent());
        Assert.Null(_scheduler.GetNextEvent());
    }
}
