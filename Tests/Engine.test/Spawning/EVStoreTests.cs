namespace Engine.test.Spawning;

using Core.Vehicles;
using Engine.test.Builders;
using Engine.Vehicles;

public class EVStoreTests
{
    [Fact]
    public void TryAllocate()
    {
        var evStore = new EVStore(500);
        Span<int> evIndexes = stackalloc int[500];
        var success = evStore.TryAllocate(
                500,
                (_, ref ev) => ev = TestData.EV(),
                evIndexes);

        Assert.True(success);
        Assert.Equal(0, evStore.AvailableCapacity());
        Assert.False(evStore.TryAllocate(
                1,
                (_, ref _) => throw new InvalidOperationException("Callback should not be invoked when allocation fails.")));

        for (var i = 0; i < 500; i++)
            Assert.Equal(1f, evStore.Get(evIndexes[i]).Preferences.PriceSensitivity);


        evStore.Free(evIndexes[0]);
        var realloc = evStore.TryAllocate(1, (_, ref _) => TestData.EV());

        Assert.True(realloc);
        Assert.Equal(0, evStore.AvailableCapacity());
    }

    [Fact]
    public void SetGet()
    {
        var evStore = new EVStore(1);
        var success = evStore.TryAllocate(1, (_, ref ev) => ev = TestData.EV());

        Assert.True(success);

        var ev = evStore.Get(0);
        Assert.Equal(1.0f, ev.Preferences.PriceSensitivity);

        var newEv = TestData.EV(preferences: new Preferences(2, 0, 0));
        evStore.Set(0, ref newEv);

        ev = evStore.Get(0);
        Assert.Equal(2.0f, ev.Preferences.PriceSensitivity);
    }
}
