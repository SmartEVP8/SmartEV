namespace Engine.GeoMath;

using Core.Shared;

/// <summary>
/// This class provides methods for calculating distances and bearings between
/// positions on the Earth's surface using the Haversine formula.
/// </summary>
public static class GeoMath
{
    /// <summary>
    /// Calculates the Haversine distance between two positions on the Earth's surface.
    /// https://www.ancientportsantiques.com/wp-content/uploads/Documents/ETUDESarchivees/MedNavigationRoutes/MedNav/TrigoSpherique.pdf#page=1.
    /// </summary>
    /// <param name="a">1st Postion.</param>
    /// <param name="b">2nd Postion.</param>
    /// <returns>Returns the distance between the 2 positions in km.</returns>
    private const double DegToRad = Math.PI / 180.0;
    public static double HaversineDistance(Position a, Position b)
    {
        var dLat = ToRad(b.Latitude - a.Latitude);
        var dLon = ToRad(b.Longitude - a.Longitude);
        var lat1 = ToRad(a.Latitude);
        var lat2 = ToRad(b.Latitude);

        var sinDLat = Math.Sin(dLat / 2);
        var sinDLon = Math.Sin(dLon / 2);

        var h = (sinDLat * sinDLat) +
                (Math.Cos(lat1) * Math.Cos(lat2) * sinDLon * sinDLon);

        return 6371.0 * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    /// <summary>
    /// Calculates the bearing from one position to another.
    /// The bearing is the angle between the north direction and the line connecting the two positions, measured in radians.
    /// https://www.ancientportsantiques.com/wp-content/uploads/Documents/ETUDESarchivees/MedNavigationRoutes/MedNav/TrigoSpherique.pdf#page=3.
    /// </summary>
    /// <param name="from">The position the bearing should start from.</param>
    /// <param name="to">The position the bearing is going to end on.</param>
    /// <returns>Returns the bearing in radians.</returns>
    public static double Bearing(Position from, Position to)
    {
        var lat1 = ToRad(from.Latitude);
        var lat2 = ToRad(to.Latitude);
        var dLon = ToRad(to.Longitude - from.Longitude);

        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = (Math.Cos(lat1) * Math.Sin(lat2)) -
                (Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon));

        return Math.Atan2(y, x);
    }

    public static bool IsInRadius(Position point, Position waypoint1, Position waypoint2, double radius)
    {
        var distToStation = HaversineDistance(point, waypoint1);
        if (distToStation <= radius) return true; // Quick check for stations near the waypoint

        // Calculate the length of the line segment between the two waypoints and the distance from the first waypoint to the station
        var segmentLength = HaversineDistance(waypoint1, waypoint2);

        if (segmentLength == 0) return false;

        // Calculate the bearing from waypoint1 to waypoint2 and from waypoint1 to the station
        var segmentBearing = Bearing(waypoint1, waypoint2);
        var stationBearing = Bearing(waypoint1, point);

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

    private static double ToRad(double degrees) => degrees * DegToRad;

}
