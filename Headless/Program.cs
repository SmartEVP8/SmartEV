using Core.Charging;
using Engine.Polyline;
namespace Headless;

using Core.Spawning;
using Core.Routing;
using Core.Shared;

using Engine;
using Engine.Parsers;
using Engine.Grid;
using Core.Utils;

public static class Program
{
    public static async Task Main()
    {
        var router = new OSRMRouter("../data/osrm/output.osrm");

        var route = router.QuerySingleDestination(9.935932, 57.046707, 10.2000, 56.1500);

        var polyline = route.polyline;
        var path = Polyline6ToPoints.DecodePolyline(polyline);

        var stations = new List<Station>
        {
            new Station(1, "Station1", "Address1", new Position(10.0, 56.5), null, 50f, new Random()),
            new Station(2, "Station2", "Address2", new Position(10.5, 56.0), null, 50f, new Random()),
            new Station(3, "Station3", "Address3", new Position(9.5, 56.0), null, 50f, new Random()),
            new Station(4, "Station4", "Address4", new Position(10.0, 55.5), null, 50f, new Random()),
            new Station(5, "Station5", "Address5", new Position(9.0, 56.0), null, 50f, new Random()),
            new Station(6, "Station6", "Address6", new Position(10.0, 56.0), null, 50f, new Random()),
            new Station(7, "Station7", "Address7", new Position(10.2, 56.2), null, 50f, new Random()),
            new Station(8, "Station8", "Address8", new Position(10.3, 56.3), null, 50f, new Random()),
            new Station(9, "Station9", "Address9", new Position(10.4, 56.4), null, 50f, new Random()),
            new Station(10, "Station10", "Address10", new Position(10.5, 56.5), null, 50f, new Random()),
            new Station(11, "Station11", "Address11", new Position(10.6, 56.6), null, 50f, new Random()),
            new Station(12, "Station12", "Address12", new Position(10.7, 56.7), null, 50f, new Random()),
            new Station(13, "Station13", "Address13", new Position(10.8, 56.8), null, 50f, new Random()),
            new Station(14, "Station14", "Address14", new Position(10.9, 56.9), null, 50f, new Random()),
            new Station(15, "Station15", "Address15", new Position(11.0, 57.0), null, 50f, new Random()),
        };

        var StreeStations = PolylineBuffer.BuildIndex(stations);

        var nearbyStations = PolylineBuffer.StationsInPolyline(StreeStations, path, 50);

        Console.WriteLine("Stations within 50 km of the route:");
        foreach (var station in nearbyStations)
        {
            Console.WriteLine($"at {station.Position.Longitude}, {station.Position.Latitude}");
        }
    }
}
