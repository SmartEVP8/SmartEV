namespace Engine.Test.Events.Middleware
{
    using Core.Shared;
    using Core.test.Builders;
    using Core.Vehicles;
    using Engine.Events;
    using Engine.Events.Middleware;
    using Engine.Init;
    using Engine.Routing;
    using Engine.test.Builders;
    using Engine.Vehicles;

    public class FindCandidateStationServiceTest
    {
        private readonly IFindCandidateStationService _service;
        private readonly EVStore _evStore;

        public FindCandidateStationServiceTest()
        {
            _evStore = new EVStore(10);
            var stations = CoreTestData.Stations(
                [
                    (0, 10.2039, 56.1629),
                    (1, 12.6, 55.7),
                    (2, 10.2045, 56.1635),
                    (3, 11.5, 54.5),
                    (4, 10.22, 56.19),
                ]);
            var spatialGrid = EngineTestData.BuildSpatialGrid(stations);

            float[] durations = [10f, 7200f, 25f, 9000f, 180f];
            float[] distances = [500f, 1900f, 130f, 2200f, 300f];

            var stubRouter = new StubRouter(durations, distances);
            _service = new FindCandidateStationService(
                stubRouter,
                stations,
                spatialGrid,
                _evStore,
                EngineTestData.StationService(stations, new EventScheduler(), _evStore), EngineConfiguration.CreateDefaultSettings().ChargeBufferPercent);
        }

        [Fact]
        public async Task PreComputeCandidateStation_ReturnsExpectedCandidates_WithDeterministicData()
        {
            var waypoints = new List<Position>
            {
                new(10.2035, 56.1625),
                new(10.2050, 56.1640),
            };

            var battery = CoreTestData.Battery(
                capacity: 100,
                maxChargeRate: 100,
                stateOfCharge: 0.3f);
            var preferences = CoreTestData.Preferences(
                MinAcceptableCharge: 0.1f,
                MaxPathDeviation: 100.0f);
            var ev = CoreTestData.EV(
                waypoints,
                battery,
                preferences,
                distanceMeter: 300_000f);
            _evStore.TryAllocate((int _, ref EV e) => { e = ev; }, out var evId);

            var action = _service.PreComputeCandidateStation();
            var e = new FindCandidateStations(evId, 0);

            action(e);
            var candidates = await _service.GetCandidateStationFromCache(evId);

            var expected = new Dictionary<ushort, DurToStationAndDest>
            {
                { 0, new DurToStationAndDest(10.0f, 0f, 300_000f, 500f) },
                { 2, new DurToStationAndDest(25f, 0f, 300_000f, 130f) },
                { 4, new DurToStationAndDest(180f, 0f, 300_000f, 300f) },
            };
            Assert.Equal(expected, candidates);
        }

        private class StubRouter(float[] durations, float[] distances) : IOSRMRouter
        {
            private readonly float[] _durations = durations;
            private readonly float[] _distances = distances;

            public RoutingLegsResult QueryStationsWithDest(
                double lon,
                double lat,
                double destLon,
                double destLat,
                ushort[] stationIds)
            {
                var toStationDurations = new float[stationIds.Length];
                var toStationDistances = new float[stationIds.Length];
                var toDestDurations = new float[stationIds.Length];
                var toDestDistances = new float[stationIds.Length];

                for (var i = 0; i < stationIds.Length; i++)
                {
                    toStationDurations[i] = _durations[stationIds[i]];
                    toStationDistances[i] = _distances[stationIds[i]];
                    toDestDurations[i] = 0f;
                    toDestDistances[i] = 300_000f;
                }

                return new RoutingLegsResult(
                    new RoutingLeg(toStationDurations, toStationDistances),
                    new RoutingLeg(toDestDurations, toDestDistances));
            }

            public void Dispose() => throw new NotImplementedException();

            public RoutingResult QueryPointsToPoints(double[] from, double[] to) =>
                throw new NotImplementedException();

            public RouteSegment QuerySingleDestination(
                double fromLon,
                double fromLat,
                double toLon,
                double toLat) => throw new NotImplementedException();

            public RouteSegment QueryDestinationWithStop(
                double fromLon,
                double fromLat,
                double stopLon,
                double stopLat,
                double toLon,
                double toLat,
                ushort stopStationId) => throw new NotImplementedException();
        }
    }
}
