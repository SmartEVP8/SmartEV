namespace Engine.test.Metrics;

using Engine.test.Builders;
using Engine.Events;
using Core.Shared;
using Core.Charging;
using Engine.Vehicles;
using Engine.Init;

public class StationSnapshotMetricTests
{
        [Fact]
        public void CollectAllSnapshots_CapturesAndResetsStationCounters()
        {
                // 1. Setup using TestData builders
                var scheduler = new EventScheduler();
                var evStore = new EVStore(10);
                var station = TestData.Station(id: 1);
                var stations = new Dictionary<ushort, Station> { [1] = station };

                // Use the builder - it handles metrics, integrators, and pathfinding for you
                var settings = TestData.DefaultSettings();
                var service = TestData.StationService(stations, scheduler, evStore, settings);

                // 2. Act: Trigger reservations through the service
                // These calls increment the internal '_windowReservations' dictionary in StationService
                service.HandleReservationRequest(new ReservationRequest(EVId: 0, StationId: 1, Time: 0, DurationToStation: 100));
                service.HandleReservationRequest(new ReservationRequest(EVId: 1, StationId: 1, Time: 10, DurationToStation: 100));
                service.HandleReservationRequest(new ReservationRequest(EVId: 2, StationId: 1, Time: 20, DurationToStation: 100));

                // 3. Act: Collect Snapshot
                var simTime = new Time(3600);
                var (_, stationMetrics) = service.CollectAllSnapshots(simTime);
                var metric = stationMetrics.First(m => m.StationId == 1);

                // 4. Assert
                Assert.Equal(3u, metric.Reservations);

                // 5. Verify Reset: Collecting again should be zero
                var (_, secondSnapshot) = service.CollectAllSnapshots(new Time(7200));
                Assert.Equal(0u, secondSnapshot.First().Reservations);
        }
}