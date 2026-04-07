namespace Engine.Test.Events.Middleware
{
    using Core.Shared;
    using Core.Vehicles;
    using Engine.Events;
    using Engine.Events.Middleware;
    using Engine.Grid;
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
            var stations = TestData.Stations([
                (0, 10.2039, 56.1629),
                (1, 12.6, 55.7),
                (2, 10.2045, 56.1635),
                (3, 11.5, 54.5),
                (4, 10.22, 56.19)
            ]);
            var spatialGrid = TestData.BuildSpatialGrid(stations);

            float[] durations = [10f, 7200f, 25f, 9000f, 180f];
            float[] distances = [50f, 1900f, 130f, 2200f, 300f];
            var stubRouter = new StubRouter(durations, distances);
            _service = new FindCandidateStationService(stubRouter, stations, spatialGrid, _evStore);
        }

        [Fact]
        public async Task PreComputeCandidateStation_ReturnsExpectedCandidates_WithDeterministicData()
        {
            var waypoints = new List<Position> { new(10.2035, 56.1625), new(10.2050, 56.1640) };

            var battery = TestData.Battery(capacity: 100, maxChargeRate: 100, stateOfCharge: 1.0f);
            var preferences = TestData.Preferences(MinAcceptableCharge: 0.1f, MaxPathDeviation: 100.0f);
            var ev = TestData.EV(waypoints, battery, preferences);
            _evStore.TryAllocate((int _, ref EV e) => { e = ev; }, out var evId);

            var action = _service.PreComputeCandidateStation();
            var e = new FindCandidateStations(evId, 0);

            action(e);
            var candidates = await _service.GetCandidateStationFromCache(evId);

            var expected = new Dictionary<ushort, float> { { 0, 10.0f }, { 2, 25 }, { 4, 180 } };
            Assert.Equal(expected, candidates);
        }

        private class StubRouter(float[] durations, float[] distances) : IOSRMRouter
        {
            private readonly float[] _durations = durations;
            private readonly float[] _distances = distances;

            public RoutingResult QueryStationsWithDest(double lon, double lat, double destLon, double destLat, ushort[] stationIds)
            {
                var durations = new float[stationIds.Length];
                var distances = new float[stationIds.Length];
                for (int i = 0; i < stationIds.Length; i++)
                {
                    durations[i] = _durations[stationIds[i]];
                    distances[i] = _distances[stationIds[i]];
                }

                return new RoutingResult(durations, distances);
            }

            public void Dispose() => throw new NotImplementedException();

            public RoutingResult QueryPointsToPoints(double[] from, double[] to) => throw new NotImplementedException();

            public RouteSegment QuerySingleDestination(double fromLon, double fromLat, double toLon, double toLat) => throw new NotImplementedException();

            public RouteSegment QueryDestinationWithStop(double fromLon, double fromLat, double stopLon, double stopLat, double toLon, double toLat, ushort stopStationId) => throw new NotImplementedException();
        }
    }
}
