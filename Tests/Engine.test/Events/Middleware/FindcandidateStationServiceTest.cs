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
        [Fact]
        public async Task PreComputeCandidateStation_ReturnsExpectedCandidates_WithDeterministicData()
        {
            var stations = TestData.Stations(
                (1, 0.5, 0.5),
                (2, 10.0, 10.0),
                (3, 1.2, 0.9),
                (4, 8.0, 8.0));

            var evStore = new EVStore(10);

            var waypoints = new List<Position> { new(0, 0), new(1, 1) };

            var battery = TestData.Battery(capacity: 100, maxChargeRate: 100, stateOfCharge: 1.0f);
            var preferences = TestData.Preferences(MinAcceptableCharge: 0.1f, MaxPathDeviation: 100.0f);
            var ev = TestData.EV(waypoints, battery, preferences);
            evStore.TryAllocate((int _, ref EV e) => { e = ev; }, out var evId);

            var stubRouter = new StubRouter(
                [60.0f, 1200.0f, 45.0f, 1000f],
                [1.0f, 20.0f, 0.8f, 15.0f]);

            var spatialGrid = new MockSpatialGrid(stations.Keys);

            var service = new FindCandidateStationService(stubRouter, stations, spatialGrid, evStore);
            var action = service.PreComputeCandidateStation();
            var e = new FindCandidateStations(evId, 0);

            action(e);
            var candidates = await service.GetCandidateStationFromCache(evId);

            var expected = new Dictionary<ushort, float> { { 1, 60.0f }, { 3, 1200f } };
            Assert.Equal(expected, candidates);
        }

        private class StubRouter(float[] durations, float[] distances) : IOSRMRouter
        {
            private readonly float[] _durations = durations;
            private readonly float[] _distances = distances;

            public RoutingResult QueryStationsWithDest(double lon, double lat, double destLon, double destLat, ushort[] stationIds) => new(_durations, _distances);

            public void Dispose() => throw new NotImplementedException();

            public RoutingResult QueryPointsToPoints(double[] from, double[] to) => throw new NotImplementedException();

            public RouteSegment QuerySingleDestination(double fromLon, double fromLat, double toLon, double toLat) => throw new NotImplementedException();

            public RouteSegment QueryDestinationWithStop(double fromLon, double fromLat, double stopLon, double stopLat, double toLon, double toLat, ushort stopStationId) => throw new NotImplementedException();
        }

        // Minimal mock for ISpatialGrid
        private class MockSpatialGrid(IEnumerable<ushort> stationIds) : ISpatialGrid
        {
            private readonly List<ushort> _stationIds = [.. stationIds];

            public List<ushort> GetStationsAlongPolyline(List<Position> waypoints, double radius) => _stationIds;
        }
    }
}
