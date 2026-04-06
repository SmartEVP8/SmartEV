namespace Engine.test.Metrics;

using System.Linq;
using Engine.Events;
using Engine.test.Builders;
using Engine.Metrics.Snapshots;
using Core.Shared;
using Engine.Vehicles;

public class StationSnapshotMetricTests
{
        [Fact]
        public void CollectAllSnapshots_CapturesAndResetsStationCounters()
        {
                var scheduler = new EventScheduler();
                var evStore = new EVStore(3);
                var stations = TestData.Stations((1, 1.0, 1.0));
                var service = TestData.StationService(stations, scheduler, evStore);
                var ev1 = TestData.EV();
                evStore.Set(0, ref ev1);
                var ev2 = TestData.EV();
                evStore.Set(1, ref ev2);
                var ev3 = TestData.EV();
                evStore.Set(2, ref ev3);

                service.HandleReservationRequest(new ReservationRequest(EVId: 0, StationId: 1, Time: new Time(0), DurationToStation: 10));
                service.HandleReservationRequest(new ReservationRequest(EVId: 1, StationId: 1, Time: new Time(0), DurationToStation: 10));
                service.HandleReservationRequest(new ReservationRequest(EVId: 2, StationId: 1, Time: new Time(0), DurationToStation: 10));

                service.HandleCancelRequest(new CancelRequest(EVId: 2, StationId: 1, Time: new Time(10)));

                var simTime = new Time(3600);

                // First collection should capture 3 reservations and 1 cancellation
                var (_, stationMetrics) = service.CollectAllSnapshots(simTime);
                var metric = stationMetrics.First(m => m.StationId == 1);

                Assert.Equal(3u, metric.Reservations);
                Assert.Equal(1u, metric.Cancellations);

                // Second collection should reflect that the window counters reset to 0
                var (_, nextStationMetrics) = service.CollectAllSnapshots(simTime + 3600);
                var nextMetric = nextStationMetrics.First(m => m.StationId == 1);

                Assert.Equal(0u, nextMetric.Reservations);
                Assert.Equal(0u, nextMetric.Cancellations);
        }
}