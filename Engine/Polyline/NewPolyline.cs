namespace Engine.Polyline;

using Core.Charging;
using Core.Shared;
using Engine.GeoMath;


public static class NewPolyline
{
    public static List<ushort> StationsInPolyline(
        List<Station> stations,
        Paths path,
        double radius,
        double latSize,
        double lonSize)
    {
        var radiusInLat = radius / latSize;
        var radiusInLon = radius / lonSize;

        var indexesOfValidStations = new List<ushort>();

        for (var i = 0; i < path.Waypoints.Count - 1; i++)
        {
            var wp = path.Waypoints[i];
            var wp2 = path.Waypoints[i + 1];
            var minLat = Math.Min(wp.Latitude, wp2.Latitude) - radiusInLat;
            var maxLat = Math.Max(wp.Latitude, wp2.Latitude) + radiusInLat;
            var minLon = Math.Min(wp.Longitude, wp2.Longitude) - radiusInLon;
            var maxLon = Math.Max(wp.Longitude, wp2.Longitude) + radiusInLon;

            foreach (var station in stations)
            {
                if (IsStationInBox(minLat, maxLat, minLon, maxLon, station.Position))
                {
                    if (IsStationInRadius(station.Position, wp, wp2, radius))
                    {
                        indexesOfValidStations.Add(station.id);
                        break;
                    }
                }
            }
        }

        return indexesOfValidStations;
    }

    private static bool IsStationInBox(double minLat, double maxLat, double minLon, double maxLon, Position stationPos)
    {
        return stationPos.Latitude >= minLat &&
               stationPos.Latitude <= maxLat &&
               stationPos.Longitude >= minLon &&
               stationPos.Longitude <= maxLon;
    }
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
