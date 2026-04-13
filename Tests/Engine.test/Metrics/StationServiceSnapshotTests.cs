namespace Engine.test.Metrics;

using Core.Charging;
using Core.Shared;
using Engine.Events;
using Engine.test.Builders;
using Core.test.Builders;
using Engine.Vehicles;
using Engine.Metrics.Snapshots;
using Engine.Services.StationServiceHelpers;
public class StationServiceSnapshotTests
{
    /*  [Fact]
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
  */
    [Theory]
    [InlineData(120, 60.0, 0.5f)] // 120kW charger, 60kWh delivered in 1 hr = 50% utilization
    [InlineData(150, 150.0, 1.0f)] // 150kW charger, 150kWh delivered in 1 hr = 100% utilization
    [InlineData(50, 0.0, 0.0f)] // 50kW charger, 0kWh delivered in 1 hr = 0% utilization
    public void Collect_CalculatesUtilizationAndAggregatesCorrectly(
            ushort chargerMaxKw,
            double deliveredKwh,
            float expectedUtilization)
    {
        var snapshotInterval = new Time(3600000);
        var collector = new StationMetricsCollector(snapshotInterval);

        var expectedQueueSize = 2;
        var expectedReservations = 5u;
        var expectedCancellations = 1u;

        var charger = CoreTestData.SingleCharger(1, maxPowerKW: chargerMaxKw);
        var station = CoreTestData.Station(1, chargers: [charger]);

        var chargerState = new ChargerState(charger, 1)
        {
            Window = new ChargerWindow
            {
                DeliveredKWh = deliveredKwh,
                HadActivity = deliveredKwh > 0,
            },
        };

        for (var i = 0; i < expectedQueueSize; i++)
        {
            chargerState.Queue.Enqueue((i, EngineTestData.ConnectedEV(i, 0.2, 0.8)));
        }

        var stationChargers = new Dictionary<ushort, List<ChargerState>> { [1] = [chargerState] };
        var stationIndex = new Dictionary<ushort, Station> { [1] = station };
        var windowReservations = new Dictionary<ushort, uint> { [1] = expectedReservations };
        var windowCancellations = new Dictionary<ushort, uint> { [1] = expectedCancellations };

        var (chargers, stations) = collector.Collect(
            new Time(3600), stationChargers, stationIndex, windowReservations, windowCancellations);

        var cs = chargers.Single();
        var ss = stations.Single();

        Assert.Equal(deliveredKwh, cs.DeliveredKW);
        Assert.Equal(expectedUtilization, cs.Utilization);
        Assert.Equal(expectedQueueSize, cs.QueueSize);

        Assert.Equal(deliveredKwh, ss.TotalDeliveredKWh);
        Assert.Equal(chargerMaxKw, ss.TotalMaxKWh);
        Assert.Equal(expectedQueueSize, ss.TotalQueueSize);
        Assert.Equal(expectedReservations, ss.Reservations);
        Assert.Equal(expectedCancellations, ss.Cancellations);
    }
}
