namespace Core.Shared;

/// <summary>
/// Represents a geographic position with longitude and latitude coordinates.
/// </summary>
/// <param name="Longitude">The longitude coordinate.</param>
/// <param name="Latitude">The latitude coordinate.</param>
public readonly record struct Position(double Longitude, double Latitude) { }

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
