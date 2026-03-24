namespace Engine.Routing;

using Core.Shared;

/// <summary>
/// Defines an interface for a matrix router that can compute distances and durations between multiple source and destination points.
/// </summary>
public interface IMatrixRouter
{
    /// <summary>
    /// Queries the distance matrix between a row of grid cells and all cities.
    /// Returns a flat array indexed as [cityIndex + (cityCount * cellIndex)].
    /// </summary>
    /// <param name="srcCoords"> Source coordinates.</param>
    /// <param name="dstCoords"> Destination coordinates.</param>
    /// <returns>Array of distances from each cell to each city, indexed as [cityIndex + (cityCount * cellIndex)].</returns>
    public (float[] durations, float[] distances) QueryPointsToPoints(double[] srcCoords, double[] dstCoords);

    /// <summary> Queries the distance matrix between a row of grid cells and all cities.</summary>
    /// <param name="sources"> Source positions.</param>
    /// <param name="destinations"> Destination positions.</param>
    /// <returns> Array of distances from each cell to each city, indexed as [cityIndex + (cityCount * cellIndex)]. </returns>
    (float[] durations, float[] distances) QueryPointsToPoints(IEnumerable<Position> sources, IEnumerable<Position> destinations)
        => QueryPointsToPoints(sources.ToFlatArray(), destinations.ToFlatArray());
}
