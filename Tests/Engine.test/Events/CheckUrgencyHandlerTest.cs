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
            efficiency: 2);

    private CheckUrgencyHandler MakeHandler() =>
        new(_scheduler, _evStore, 5, new Random(42));

    [Fact]
    public void LowUrgencySchedulesCheckUrgency()
    {
        var ev = MakeEV(stateOfCharge: 50f);
        _evStore.Set(1, ref ev);

        MakeHandler().Handle(new CheckUrgency(1, 0));

        Assert.IsType<CheckUrgency>(_scheduler.GetNextEvent());
        Assert.Null(_scheduler.GetNextEvent());
    }

    [Fact]
    public void HighUrgencySchedulesFindCandidate()
    {
        var ev = MakeEV(stateOfCharge: 4f);
        _evStore.Set(1, ref ev);

        MakeHandler().Handle(new CheckUrgency(1, 0));

        Assert.IsType<FindCandidateStations>(_scheduler.GetNextEvent());
        Assert.IsType<CheckUrgency>(_scheduler.GetNextEvent());
        Assert.Null(_scheduler.GetNextEvent());
    }
}
