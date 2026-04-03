namespace Core.Shared;

/// <summary>
/// Represents a geographic position with longitude and latitude coordinates.
/// </summary>
/// <param name="Longitude">The longitude coordinate.</param>
/// <param name="Latitude">The latitude coordinate.</param>
public sealed record Position(double Longitude, double Latitude)
{
    private const double _tolerance = 0.00001; // ~1 meter at equator

    /// <inheritdoc/>
    public bool Equals(Position? other)
    {
        if (other is null)
            return false;

        return Math.Abs(Longitude - other.Longitude) < _tolerance &&
               Math.Abs(Latitude - other.Latitude) < _tolerance;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Bucket positions into tolerance-sized cells for hash consistency
        var lonBucket = (int)(Longitude / _tolerance);
        var latBucket = (int)(Latitude / _tolerance);
        return HashCode.Combine(lonBucket, latBucket);
    }
}
