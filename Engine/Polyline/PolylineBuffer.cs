namespace Engine.Polyline;

using Core.Shared;
using Core.Charging;
using System.Collections.Concurrent;
using Engine.GeoMath;

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
    /// <summary>
    /// Checks if any station is within a certain radius of the polyline defined by the path's waypoints.
    /// </summary>
    /// <param name="stations">Should contain all stations on the map.</param>
    /// <param name="path">The polyline we are going to check if the stations are near.</param>
    /// <param name="radius">The radius we what to check if the stations are within.</param>
    /// <returns>Returns a new list of the Stations within the set radius.</returns>
    public static List<Station> StationsInPolyline(List<Station> stations, Paths path, double radius)
    {
        var waypoints = path.Waypoints;
        var result = new ConcurrentBag<Station>();
        Parallel.ForEach(stations, s =>
         {
             for (var i = 0; i < waypoints.Count; i++)
             {
                 // Check if the station is within the radius of the current waypoint
                 if (GeoMath.HaversineDistance(s.Position, waypoints[i]) <= radius)
                 {
                     result.Add(s);
                     break;
                 }

                 if (i < waypoints.Count - 1)
                 {
                     // Check if the station is within the radius of the line segment between the current waypoint and the next waypoint
                     if (IsStationInCorridor(s.Position, waypoints[i], waypoints[i + 1], radius))
                     {
                         result.Add(s);
                         break;
                     }
                 }
             }
         });
        return result.ToList();
    }

    /// <summary>
    /// Determines if a point is within a corridor defined by two waypoints and a radius.
    /// This is used to check if a station is within the radius of the line segment between two waypoints, not just at the waypoints themselves.
    /// </summary>
    private static bool IsStationInCorridor(Position station, Position waypoint1, Position waypoint2, double radius)
    {
        // Calculate the length of the line segment between the two waypoints and the distance from the first waypoint to the station
        var segmentLength = GeoMath.HaversineDistance(waypoint1, waypoint2);
        var distToStart = GeoMath.HaversineDistance(waypoint1, station);
        if (segmentLength == 0) return false;

        // Calculate the bearing from waypoint1 to waypoint2 and from waypoint1 to the station
        var segmentBearing = GeoMath.Bearing(waypoint1, waypoint2);
        var pointBearing = GeoMath.Bearing(waypoint1, station);

        // Calculate the angle between the segment and the station
        var angle = pointBearing - segmentBearing;

        // Distance along and perpendicular to the segment
        var along = distToStart * Math.Cos(angle);
        var perp = Math.Abs(distToStart * Math.Sin(angle));

        // Must be between the two circles and within radius
        return along >= 0 && along <= segmentLength && perp <= radius;
    }
}
