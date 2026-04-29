namespace Engine.test.Vehicles;

using Core.Shared;
using Core.Vehicles;
using Engine.Events;
using Engine.test.Builders;
using Engine.Vehicles;

public class EVPopulatorTests
{
    [Fact]
    public void CreateEVsSameObjects2Iterations()
    {
        var journeySamplerProvider = EngineTestData.JourneySamplerProvider();
        var fakeScheduler = new FakeScheduler();
        var evOptions = new EVOptions();
        var evStore1 = new Dictionary<int, EV>();
        var evStore2 = new Dictionary<int, EV>();

        var evPopulator1 = new EVPopulator(new EVFactory(new Random(1), journeySamplerProvider, EngineTestData.OSRMRouter, evOptions), evStore1, fakeScheduler);
        var evPopulator2 = new EVPopulator(new EVFactory(new Random(1), journeySamplerProvider, EngineTestData.OSRMRouter, evOptions), evStore2, fakeScheduler);

        evPopulator1.CreateEVs(100, 3600);
        evPopulator2.CreateEVs(100, 3600);

        var keys = evStore1.Keys.Order().ToList();
        Assert.Equal(100, keys.Count);
        foreach (var key in keys)
        {
            Assert.Equal(evStore1[key].ToString(), evStore2[key].ToString());
        }
    }

    private class FakeScheduler : IEventScheduler
    {
        public Time CurrentTime = 0;

        public List<Event> Events = [];

        private uint _count = 0;

        Time IEventScheduler.CurrentTime => CurrentTime;

        public uint ScheduleEvent(Event e)
        {
            Events.Append(e);
            return _count++;
        }

        public Event? GetNextEvent()
        {
            if (Events.Count == 0)
                return null;

            var nextEvent = Events[0];
            Events.RemoveAt(0);
            return nextEvent;
        }
    }
}
