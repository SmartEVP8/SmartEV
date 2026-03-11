namespace Engine.Polyline;

using Core.Charging;
using Core.Shared;
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
    public static List<Station> StationsInPolyline(
        List<Station> stations,
        Paths path,
        double radius,
        double latSize,
        double lonSize)
    {

        // Compute bounding box of the path with added radius buffer
        var minLat = double.MaxValue;
        var maxLat = double.MinValue;
        var minLon = double.MaxValue;
        var maxLon = double.MinValue;
        var radiusInLat = radius / latSize;
        var radiusInLon = radius / lonSize;

        foreach (var wp in path.Waypoints)
        {
            if (wp.Latitude < minLat) minLat = wp.Latitude;
            if (wp.Latitude > maxLat) maxLat = wp.Latitude;
            if (wp.Longitude < minLon) minLon = wp.Longitude;
            if (wp.Longitude > maxLon) maxLon = wp.Longitude;
        }

        var candidates = stations
            .Where(s =>
                s.Position.Latitude >= minLat - radiusInLat &&
                s.Position.Latitude <= maxLat + radiusInLat &&
                s.Position.Longitude >= minLon - radiusInLon &&
                s.Position.Longitude <= maxLon + radiusInLon)
            .ToList();

        var stationsCloseToLine = new List<Station>();
        // Parallelise over stations, not segments — allows early exit per station
        foreach (var station in candidates)
        {
            for (var i = 0; i < path.Waypoints.Count - 1; i++)
            {
                if (
                    IsStationInRadius(
                        station.Position,
                        path.Waypoints[i],
                        path.Waypoints[i + 1],
                        radius))
                {
                    stationsCloseToLine.Add(station);
                    break;
                }
            }

            // Check final waypoint
            if (GeoMath.HaversineDistance(station.Position, path.Waypoints[^1]) <= radius)
                stationsCloseToLine.Add(station);
        }

        return stationsCloseToLine.OrderBy(s => s.Position.Longitude).ToList();
    }

    /// <summary>
    /// Determines if a point is within a corridor defined by two waypoints and a radius.
    /// This is used to check if a station is within the radius of the line segment between two waypoints, not just at the waypoints themselves.
    /// </summary>
    private static bool IsStationInRadius(Position station, Position waypoint1, Position waypoint2, double radius)
    {
        var distToStation = GeoMath.HaversineDistance(station, waypoint1);
        if (distToStation <= radius) return true; // Quick check for stations near the waypoint

        // Calculate the length of the line segment between the two waypoints and the distance from the first waypoint to the station
        var segmentLength = GeoMath.HaversineDistance(waypoint1, waypoint2);

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
