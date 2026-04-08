namespace Engine.test.Vehicles;

using Core.Shared;
using Engine.Events;
using Engine.Spawning;
using Engine.test.Builders;
using Engine.Vehicles;

public class EVPopulatorTests
{
    [Fact]
    public void CreateEVsSameObjects2Iterations()
    {
        var journeySamplerProvider = TestData.JourneySamplerProvider();
        var fakeScheduler = new FakeScheduler();
        var evStore1 = new EVStore(100);
        var evStore2 = new EVStore(100);

        var evPopulator1 = new EVPopulator(new EVFactory(new Random(1), journeySamplerProvider, TestData.OSRMRouter), evStore1, fakeScheduler);
        var evPopulator2 = new EVPopulator(new EVFactory(new Random(1), journeySamplerProvider, TestData.OSRMRouter), evStore2, fakeScheduler);

        evPopulator1.CreateEVs(100, 3600);
        evPopulator2.CreateEVs(100, 3600);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(evStore1.Get(i).ToString(), evStore2.Get(i).ToString());
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
