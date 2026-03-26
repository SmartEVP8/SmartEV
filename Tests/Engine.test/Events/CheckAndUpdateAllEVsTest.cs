namespace Testing;

using Core.Vehicles;
using Engine.Events;
using Engine.test.Builders;
using Engine.Vehicles;
using Core.Shared;
public class CheckAndUpdateAllEVsTest
{
    private readonly EventScheduler _scheduler = new([]);
    private readonly EVStore _evStore = new(10);

    private EV MakeEV(
    float stateOfCharge = 20.1f,
    ushort efficiency = 2,
    Time originalDuration = default) =>
    TestData.EV(
        waypoints: [new Position(10.1, 10.1), new Position(10.2, 10.2)],
        battery: TestData.Battery(capacity: 50, maxChargeRate: 20, stateOfCharge: stateOfCharge),
        preferences: TestData.Preferences(PriceSensitivity: 0.5f, MinAcceptableCharge: 0.0f),
        efficiency: efficiency,
        originalDuration: originalDuration);

    [Fact]
    public void CheckAndUpdateAllTest()
    {
        var ev1 = MakeEV(stateOfCharge: 50f);
        var ev2 = MakeEV(stateOfCharge: 4f);
        _evStore.TryAllocate((_, ref ev) => ev = ev1, out var index1);
        _evStore.TryAllocate((_, ref ev) => ev = ev2, out var index2);

        new CheckAndUpdateAllEVsHandler(_scheduler, _evStore, 5, 5)
            .Handle(new CheckAndUpdateAllEVs(0));

        Assert.IsType<CheckUrgency>(_scheduler.GetNextEvent());
        Assert.IsType<CheckUrgency>(_scheduler.GetNextEvent());
        Assert.IsType<CheckAndUpdateAllEVs>(_scheduler.GetNextEvent());
    }

    [Fact]
    public void CheckAndUpdateAllTestWithOneUrgency()
    {
        var ev1 = MakeEV(efficiency: 300, stateOfCharge: 50f, originalDuration: 60);
        var ev2 = MakeEV(efficiency: 200, stateOfCharge: 20.1f, originalDuration: 60);

        _evStore.TryAllocate((_, ref ev) => ev = ev1, out _);
        _evStore.TryAllocate((_, ref ev) => ev = ev2, out _);

        new CheckAndUpdateAllEVsHandler(_scheduler, _evStore, 5, 10)
            .Handle(new CheckAndUpdateAllEVs(0));

        Assert.IsType<CheckUrgency>(_scheduler.GetNextEvent());
        Assert.IsType<CheckAndUpdateAllEVs>(_scheduler.GetNextEvent());
    }
}
