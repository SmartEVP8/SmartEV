namespace Engine.Polyline;

using Core.Charging;
using Core.Shared;
using System.Collections.Concurrent;
using Engine.GeoMath;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Geometries;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// This class provides a method to check if any station is within a certain radius of a polyline defined by a path's waypoints.
/// It uses the Haversine formula to calculate distances and bearings between points on the Earth's surface.
/// The method is designed to be efficient by using parallel processing to check multiple stations simultaneously.
/// The Bearing method calculates the angle between two points, which is used to determine the position of a station relative to the polyline.
/// The IsPointInBox method checks if a station is within a rectangular area defined by two waypoints and the specified radius,
/// which helps to account for stations that are near the line segment between waypoints rather than just at the waypoints themselves.
/// </summary>
public static class PolylineBuffer
{

    // Build once, reuse across calls
    public static STRtree<Station> BuildIndex(List<Station> stations)
    {
        var tree = new STRtree<Station>();
        foreach (var s in stations)
        {
            var pt = new Coordinate(s.Position.Longitude, s.Position.Latitude);
            tree.Insert(new Envelope(pt), s);
        }
        tree.Build();
        return tree;
    }

    /// <summary>
    /// Checks if any station is within a certain radius of the polyline defined by the path's waypoints.
    /// </summary>
    /// <param name="stations">Should contain all stations on the map.</param>
    /// <param name="path">The polyline we are going to check if the stations are near.</param>
    /// <param name="radius">The radius we what to check if the stations are within.</param>
    /// <returns>Returns a new list of the Stations within the set radius.</returns>
    public static List<Station> StationsInPolyline(
        STRtree<Station> index,
        Paths path,
        double radius)
    {
        var waypoints = path.Waypoints;
        var seen = new ConcurrentDictionary<Station, bool>();

        // Approx degree offset for the radius bounding box pre-filter
        var degOffset = radius / 100.0;

        Parallel.For(0, waypoints.Count - 1, i =>
        {
            var wp1 = waypoints[i];
            var wp2 = waypoints[i + 1];

            // Bounding box covering the segment + radius buffer
            var env = new Envelope(
                Math.Min(wp1.Longitude, wp2.Longitude) - degOffset,
                Math.Max(wp1.Longitude, wp2.Longitude) + degOffset,
                Math.Min(wp1.Latitude, wp2.Latitude) - degOffset,
                Math.Max(wp1.Latitude, wp2.Latitude) + degOffset);

            var candidates = index.Query(env);
            foreach (var s in candidates)
            {
                if (seen.ContainsKey(s)) continue;

                if (GeoMath.HaversineDistance(s.Position, wp1) <= radius ||
                    GeoMath.HaversineDistance(s.Position, wp2) <= radius ||
                    IsStationInCorridor(s.Position, wp1, wp2, radius))
                {
                    seen.TryAdd(s, true);
                }
            }
        });

        return seen.Keys.ToList();
    }

    /// <summary>
    /// Determines if a point is within a corridor defined by two waypoints and a radius.
    /// This is used to check if a station is within the radius of the line segment between two waypoints, not just at the waypoints themselves.
    /// </summary>
    private static bool IsStationInCorridor(Core.Shared.Position station, Core.Shared.Position waypoint1, Core.Shared.Position waypoint2, double radius)
    {
        // Calculate the length of the line segment between the two waypoints and the distance from the first waypoint to the station
        var segmentLength = GeoMath.HaversineDistance(waypoint1, waypoint2);
        var distToStation = GeoMath.HaversineDistance(waypoint1, station);
        if (segmentLength == 0) return false;

        // Calculate the bearing from waypoint1 to waypoint2 and from waypoint1 to the station
        var segmentBearing = GeoMath.Bearing(waypoint1, waypoint2);
        var stationBearing = GeoMath.Bearing(waypoint1, station);

        // Calculate the angle between the segment and the station
        var angle = stationBearing - segmentBearing;

        // Normalize
        while (angle > Math.PI) angle -= 2 * Math.PI;
        while (angle < -Math.PI) angle += 2 * Math.PI;

        // Distance along and perpendicular to the segment
        var along = distToStation * Math.Cos(angle);

        // Pythagorean theorem to find the perpendicular distance
        // from the station to the line defined by the waypoints
        var perp = Math.Sqrt((distToStation * distToStation) - (along * along));


        // Must be between the two circles and within radius
        return along >= 0 && along <= segmentLength && perp <= radius;
    }
}
