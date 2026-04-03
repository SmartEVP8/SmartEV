namespace Core.Shared;

/// <summary>
/// Represents a geographic position with longitude and latitude coordinates.
/// </summary>
/// <param name="Longitude">The longitude coordinate.</param>
/// <param name="Latitude">The latitude coordinate.</param>
public record Position(double Longitude, double Latitude)
{
}
