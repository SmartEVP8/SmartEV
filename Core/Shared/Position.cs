namespace Core.Shared;

/// <summary>
/// Represents a geographic position with longitude and latitude coordinates.
/// </summary>
/// <param name="longitude">The longitude coordinate.</param>
/// <param name="latitude">The latitude coordinate.</param>
public readonly record struct Position(double longitude, double latitude)
{
    /// <summary>
    /// Gets the longitude coordinate.
    /// </summary>
    public readonly double Longitude = longitude;

    /// <summary>
    /// Gets the latitude coordinate.
    /// </summary>
    public readonly double Latitude = latitude;
}

/// <summary>
/// Provides a method to convert a collection of Position instances into a flat array of doubles.
/// </summary>
public static class PositionExtensions
{
    /// <summary>
    /// Converts an enumerable of Position instances into a flat array of doubles, where each Position is represented as a tuple of lon, lat.
    /// </summary>
    /// <param name="positions">The collection of Position instances to convert.</param>
    /// <returns>A flat array of doubles representing the positions.</returns>
    public static double[] ToFlatArray(this IEnumerable<Position> positions)
        => [.. positions.SelectMany(p => new[] { p.Longitude, p.Latitude })];
}
