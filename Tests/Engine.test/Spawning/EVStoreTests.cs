namespace Engine.test.Spawning;

using Core.Shared;
using Core.Vehicles;
using Engine.Spawning;


public class EVStoreTests
{
    [Fact]
    public void TryAllocate()
    {
        var evStore = new EVStore(500);
        Span<int> evIndexes = stackalloc int[500];
        var success = evStore.TryAllocate(500, (_, ref ev) => ev = new EV(new Battery(1, 1, 1, Socket.CCS2), new Preferences(1.5f)), evIndexes);
        Assert.True(success);
        Assert.Equal(0, evStore.AvailableCapacity());
        for (var i = 0; i < 500; i++)
            Assert.Equal(1.5f, evStore.Get(evIndexes[i]).Preferences.PriceSensitivity);

        var failure = evStore.TryAllocate(1, (_, ref _) => throw new InvalidOperationException("Callback should not be invoked when allocation fails."));
        Assert.False(failure);

        evStore.Free(evIndexes[0]);
        var realloc = evStore.TryAllocate(1, (_, ref ev) => ev = new EV(new Battery(1, 1, 1, Socket.CCS2), new Preferences(1.5f)));
        Assert.True(realloc);
        Assert.Equal(0, evStore.AvailableCapacity());
    }

    [Fact]
    public void SetGet()
    {
        var evStore = new EVStore(1);
        var success = evStore.TryAllocate(1, (_, ref ev) => ev = new EV(new Battery(1, 1, 1, Socket.CCS2), new Preferences(1.5f)));

        Assert.True(success);

        var ev = evStore.Get(0);
        Assert.Equal(1.5f, ev.Preferences.PriceSensitivity);

        var newEv = new EV(new Battery(1, 1, 1, Socket.CCS2), new Preferences(2.0f));
        evStore.Set(0, ref newEv);

        ev = evStore.Get(0);
        Assert.Equal(2.0f, ev.Preferences.PriceSensitivity);
    }
}
