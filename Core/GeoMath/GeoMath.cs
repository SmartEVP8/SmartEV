namespace Core.GeoMath;

using Core.Shared;
using Core.Helper;

/// <summary>
/// This class provides methods for calculating distances and bearings between
/// positions on the Earth's surface using the Haversine formula.
/// </summary>
public static class GeoMath
{
    /// <summary>
    /// Approximate kilometers per degree of latitude ±1% error.
    /// https://www.britannica.com/science/latitude .
    /// </summary>
    public const double KmPerLatitudeDegree = 111.32;

    /// <summary>
    /// Approximate kilometers per degree of longitude at 56° latitude ±1% error.
    /// </summary>
    public static readonly double KmPerLongtitudeDegree = 111.32 * Math.Cos(56.0 * Math.PI / 180.0);

    /// <summary>
    /// Conversion factor from degrees to radians, used in distance and bearing calculations.
    /// </summary>
    private const double DegToRad = Math.PI / 180.0;

    /// <summary>
    /// The average radius of the Earth in kilometers, used in distance calculations.
    /// </summary>
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Uses point line-segment distance to check if a point is within a certain radius of a line segment defined by two waypoints.
    /// Implemented as shown in https://www.youtube.com/watch?v=egmZJU-1zPU .
    /// </summary>
    /// <param name="point">The position to check if it's within the radius of the line segment defined by wp1 and wp2.</param>
    /// <param name="wp1">The first waypoint defining the line segment.</param>
    /// <param name="wp2">The second waypoint defining the line segment.</param>
    /// <param name="radius">The radius in kilometers that defines how close the point must be to the line segment to be considered "in radius".</param>
    /// <returns>Bool based on if the <paramref name="point"/> is in the radius of the line segment
    /// given by <paramref name="wp1"/> to <paramref name="wp2"/>.</returns>
    public static bool IsInRadius(Position point, Position wp1, Position wp2, double radius)
    {
        // We scale the longitude by the cosine of the latitude to account for the fact that
        // the distance represented by a degree of longitude varies with latitude.
        if (Math.Cos(wp1.Latitude + wp2.Latitude) == 0)
            throw Log.Error(0, 0, new ArgumentException("Waypoints cannot be at the poles where cosine of latitude is zero."), ("wp1", wp1), ("wp2", wp2));

        if (radius <= 0)
            throw Log.Error(0, 0, new ArgumentOutOfRangeException(nameof(radius), $"Radius must be positive. Received {radius}."), ("wp1", wp1), ("wp2", wp2), ("Radius", radius));

        var cosLat = Math.Cos((wp1.Latitude + wp2.Latitude) / 2.0 * Math.PI / 180.0);

        var aPoint = new Vec2(wp1.Longitude * cosLat, wp1.Latitude);
        var bPoint = new Vec2(wp2.Longitude * cosLat, wp2.Latitude);
        var pPoint = new Vec2(point.Longitude * cosLat, point.Latitude);

        var abVec = bPoint - aPoint;
        var apVec = pPoint - aPoint;

        var proj = apVec.Dot(abVec);
        var t = Math.Clamp(proj / abVec.LengthSq, 0.0, 1.0);

        var closestPoint = new Vec2(aPoint.X + (t * abVec.X), aPoint.Y + (t * abVec.Y));
        var dist = pPoint - closestPoint;

        var radiusScaled = radius / KmPerLatitudeDegree;
        return dist.LengthSq <= radiusScaled * radiusScaled;
    }

    /// <summary>
    /// Calculates the distance from the start of the segments to the point, following the segments waypoints,
    /// and checking if the point is within a certain radius of the segments.
    /// </summary>
    /// <param name="waypoints">The segments to check the distance along.</param>
    /// <param name="position">The position to check the distance to.</param>
    /// <param name="radius">The radius in kilometers that defines how close the point must be to the segments to be considered "in radius".</param>
    /// <returns>Returns the distance from the start of the segments to the point if it's within the radius of the segments, otherwise returns -1.</returns>
    public static double DistancesThroughPath(
    List<Position> waypoints, Position position, double radius)
    {
        if (radius <= 0)
            throw Log.Error(0, 0, new ArgumentOutOfRangeException(nameof(radius), $"Radius must be positive. Received {radius}."), ("Waypoints", waypoints), ("Position", position), ("Radius", radius));
        var matchIndex = -1;
        var radiusDeg = radius / KmPerLatitudeDegree;

        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var wp1 = waypoints[i];
            var wp2 = waypoints[i + 1];

            var minLat = Math.Min(wp1.Latitude, wp2.Latitude) - radiusDeg;
            var maxLat = Math.Max(wp1.Latitude, wp2.Latitude) + radiusDeg;
            if (position.Latitude < minLat || position.Latitude > maxLat)
                continue;

            if (IsInRadius(position, wp1, wp2, radius))
            {
                matchIndex = i;
                break;
            }
        }

        if (matchIndex == -1) return -1;

        var totalDistance = 0.0;
        for (var i = 0; i < matchIndex; i++)
        {
            totalDistance += EquirectangularDistance(
                waypoints[i], waypoints[i + 1]);
        }

        return totalDistance + EquirectangularDistance(
            position, waypoints[matchIndex]);
    }

    /// <summary>
    /// Calculates the distance between two positions using the equirectangular approximation,
    /// which is faster than the Haversine formula and sufficiently accurate for small distances.
    /// https://www.ancientportsantiques.com/wp-content/uploads/Documents/ETUDESarchivees/MedNavigationRoutes/MedNav/TrigoSpherique.pdf#page=2.
    /// </summary>
    /// <param name="a">Position of the first location.</param>
    /// <param name="b">Position of the second location.</param>
    /// <returns>The distance between two points in km.</returns>
    public static double EquirectangularDistance(Position a, Position b)
    {
        var lat1 = a.Latitude * DegToRad;
        var lat2 = b.Latitude * DegToRad;
        var dLon = (b.Longitude - a.Longitude) * DegToRad;

        if (Math.Cos((lat1 + lat2) / 2) == 0)
            throw Log.Error(0, 0, new ArgumentException("Positions cannot be at the poles where cosine of latitude is zero."), ("PositionA", a), ("PositionB", b));

        var x = dLon * Math.Cos((lat1 + lat2) / 2);
        var y = lat2 - lat1;

        return EarthRadiusKm * Math.Sqrt((x * x) + (y * y));
    }

    // <summary>
    // Calculates the Haversine distance between two positions on the Earth's surface.
    // https://www.ancientportsantiques.com/wp-content/uploads/Documents/ETUDESarchivees/MedNavigationRoutes/MedNav/TrigoSpherique.pdf#page=1.
    // </summary>
    // <param name="a">1st Postion.</param>
    // <param name="b">2nd Postion.</param>
    // <returns>Returns the distance between the 2 positions in km.</returns>

    /* public static double HaversineDistance(Position a, Position b)
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
     */

    // <summary>
    // Calculates the bearing from one position to another.
    // The bearing is the angle between the north direction and the line connecting the two positions, measured in radians.
    // https://www.ancientportsantiques.com/wp-content/uploads/Documents/ETUDESarchivees/MedNavigationRoutes/MedNav/TrigoSpherique.pdf#page=3.
    // </summary>
    // <param name="from">The position the bearing should start from.</param>
    // <param name="to">The position the bearing is going to end on.</param>
    // <returns>Returns the bearing in radians.</returns>
    // public static double Bearing(Position from, Position to)
    // {
    //     var lat1 = ToRad(from.Latitude);
    //     var lat2 = ToRad(to.Latitude);
    //     var dLon = ToRad(to.Longitude - from.Longitude);
    //
    //     var y = Math.Sin(dLon) * Math.Cos(lat2);
    //     var x = (Math.Cos(lat1) * Math.Sin(lat2)) -
    //             (Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon));
    //
    //     return Math.Atan2(y, x);
    // }

    // public static bool IsInRadius(Position point, Position waypoint1, Position waypoint2, double radius)
    // {
    //     var distToStation = HaversineDistance(point, waypoint1);
    //     if (distToStation <= radius) return true; // Quick check for stations near the waypoint
    //
    //     // Calculate the length of the line segment between the two waypoints and the distance from the first waypoint to the station
    //     var segmentLength = HaversineDistance(waypoint1, waypoint2);
    //
    //     if (segmentLength == 0) return false;
    //
    //     // Calculate the bearing from waypoint1 to waypoint2 and from waypoint1 to the station
    //     var segmentBearing = Bearing(waypoint1, waypoint2);
    //     var stationBearing = Bearing(waypoint1, point);
    //
    //     // Calculate the angle between the segment and the station
    //     var angle = stationBearing - segmentBearing;
    //
    //     // Normalize
    //     while (angle > Math.PI) angle -= 2 * Math.PI;
    //     while (angle < -Math.PI) angle += 2 * Math.PI;
    //
    //     // Distance along and perpendicular to the segment
    //     var along = distToStation * Math.Cos(angle);
    //
    //     // Pythagorean theorem to find the perpendicular distance
    //     // from the station to the line defined by the waypoints
    //     var perp = Math.Sqrt((distToStation * distToStation) - (along * along));
    //
    //     // Must be between the two circles and within radius
    //     return along >= 0 && along <= segmentLength && perp <= radius;
    // }

    // private static double ToRad(double degrees) => degrees * DegToRad;
}
