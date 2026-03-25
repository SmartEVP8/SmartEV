namespace Engine.test.Events.Middleware;

using Engine.Events.Middleware;
using Engine.test.Builders;
using Engine.Vehicles;
using Core.Vehicles;
using Xunit;
using Core.Shared;
using Engine.Events;
using Engine.Utils;

public class FindCandidateStationServiceTests
{
    [Fact]
    public async Task ComputeFromCache_ReturnsDurationPerStation()
    {
        var ev = TestData.EV(_path.Waypoints);
        var (store, index) = EVStoreWith(ev);
        var sut = CreateSut(store);

        sut.PreComputeCandidateStation()(new FindCandidateStations(index, default));
        var result = await sut.ComputeCandidateStationFromCache(index);

        Assert.NotEmpty(result);
        Assert.All(result.Values, duration => Assert.True(duration > 0));
    }

    [Fact]
    public async Task ComputeFromCache_Throws_WhenNeverPrecomputed()
    {
        var (store, _) = EVStoreWith(TestData.EV(_path.Waypoints));
        var sut = CreateSut(store);

        await Assert.ThrowsAsync<SkillissueException>(() =>
            sut.ComputeCandidateStationFromCache(0));
    }

    [Fact]
    public void PreCompute_Throws_WhenWrongEventType()
    {
        var (store, _) = EVStoreWith(TestData.EV(_path.Waypoints));
        var sut = CreateSut(store);

        Assert.Throws<SkillissueException>(() =>
            sut.PreComputeCandidateStation()(new FakeMiddlewareEvent()));
    }

    [Fact]
    public async Task PreCompute_CalledTwice_OverwritesPreviousResult()
    {
        var ev = TestData.EV(_path.Waypoints);
        var (store, index) = EVStoreWith(ev);
        var sut = CreateSut(store);
        var e = new FindCandidateStations(index, default);

        sut.PreComputeCandidateStation()(e);
        sut.PreComputeCandidateStation()(e);
        var result = await sut.ComputeCandidateStationFromCache(index);

        Assert.NotEmpty(result);
    }

    private record FakeMiddlewareEvent : IMiddlewareEvent
    { }

    private static readonly Paths _path = TestData.Route(9.935932, 57.046707, 12.5683, 55.6761);

    private static (EVStore store, int index) EVStoreWith(EV ev)
    {
        var store = new EVStore(1);
        var index = -1;
        store.TryAllocate(1, (i, ref e) =>
        {
            index = i;
            e = ev;
        });
        return (store, index);
    }

    private FindCandidateStationService CreateSut(EVStore store) =>
        new(TestData.OSRMRouter,
            TestData.AllStations,
            TestData.SpatialGrid,
            store);
}
