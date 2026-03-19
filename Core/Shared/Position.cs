namespace Core.Shared;

/// <summary>
/// Represents a geographic position with longitude and latitude coordinates.
/// </summary>
/// <param name="longitude">The longitude coordinate.</param>
/// <param name="latitude">The latitude coordinate.</param>
public readonly struct Position(double longitude, double latitude)
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
