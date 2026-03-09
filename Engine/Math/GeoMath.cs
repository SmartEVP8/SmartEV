namespace Engine.GeoMath;

using Core.Shared;

/// <summary>
/// This class provides methods for calculating distances and bearings between positions on the Earth's surface using the Haversine formula.
/// </summary>
public static class GeoMath
{
    /// <summary>
    /// Calculates the Haversine distance between two positions on the Earth's surface.
    /// </summary>
    /// <param name="a">1st Postion.</param>
    /// <param name="b">2nd Postion.</param>
    /// <returns>Returns the distance between the 2 positions in km.</returns>
    public static double HaversineDistance(Position a, Position b)
    {
        var earthRadius = 6371.0; // Radius of the Earth in kilometers
        var dLat = ToRad(b.Latitude - a.Latitude);
        var dLon = ToRad(b.Longitude - a.Longitude);
        var lat1 = ToRad(a.Latitude);
        var lat2 = ToRad(b.Latitude);

        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) +
                (Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        return earthRadius * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    /// <summary>
    /// Calculates the bearing from one position to another.
    /// The bearing is the angle between the north direction and the line connecting the two positions, measured in radians.
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

    private static double ToRad(double degrees) => degrees * (Math.PI / 180);

}
