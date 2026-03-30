namespace Engine.test.Metrics;

using Engine.test.Builders;
using Engine.Metrics.Snapshots;
using Core.Shared;

public class StationSnapshotMetricTests
{
        [Fact]
        public void Collect_CapturesAndResetsStationCounters()
        {
                var station = TestData.Station(id: 1);

                station.IncrementReservations();
                station.IncrementReservations();
                station.IncrementReservations(); // 3 total
                station.IncrementCancellations(); // 1 total

                var simTime = new Time(3600);

                var metric = StationSnapshotMetric.Collect(station, 10);

                Assert.Equal(3u, metric.Reservations);
                Assert.Equal(1u, metric.Cancellations);

                var (res, canc) = station.CountReservationsCancellations();
                Assert.Equal(0, res);
                Assert.Equal(0, canc);
        }
}