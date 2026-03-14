namespace Core.Shared;

public readonly struct Position(double longitude, double latitude)
{
    public readonly double Longitude = longitude;
    public readonly double Latitude = latitude;
}

public static class PositionExtensions
{
    public static double[] ToFlatArray(this IEnumerable<Position> positions)
        => [.. positions.SelectMany(p => new[] { p.Longitude, p.Latitude })];
}
