namespace Engine.Grid;

using Core.Shared;
using Engine.Spawning;

/// <summary>
/// Defines the interface for sampling journeys, which includes sampling a source and destination position for a journey.
/// </summary>
public interface IJourneySampler
{
    /// <summary>
    /// Samples a source and destination position for a journey.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <returns>A tuple containing the source and destination positions.</returns>
    (Position Source, Position Destination) SampleSourceToDest(Random random);
}

/// <summary>
/// Implements the IJourneySampler interface using Alias Sampling for efficient sampling of sources and destinations based on a grid and city centers.
/// </summary>
/// <param name="source">The alias sampler for selecting source positions.</param>
/// <param name="destinations">An array of alias samplers for selecting destination positions.</param>
/// <param name="cellCenters">An array of centers for each cell in the grid.</param>
/// <param name="cityCenters">An array of centers for each city.</param>
/// <param name="halfLat">Half the latitude range for each cell.</param>
/// <param name="halfLon">Half the longitude range for each cell.</param>
public class JourneySamplers(
    AliasSampler source,
    AliasSampler[] destinations,
    Position[] cellCenters,
    Position[] cityCenters,
    double halfLat,
    double halfLon) : IJourneySampler
{
    private readonly AliasSampler _sourceSampler = source;
    private readonly AliasSampler[] _destinationSamplers = destinations;
    private readonly Position[] _cityCenters = cityCenters;
    private readonly Position[] _cellCenters = cellCenters;
    private readonly double _halfLat = halfLat;
    private readonly double _halfLon = halfLon;

    /// <summary>
    /// Samples a source and destination position for a journey.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <returns>A tuple containing the source and destination positions.</returns>
    public (Position Source, Position Destination) SampleSourceToDest(Random random)
    {
        var sourceIndex = _sourceSampler.Sample(random);
        var source = SampleInCell(random, sourceIndex);

        var destIndex = _destinationSamplers[sourceIndex].Sample(random);
        var dest = _cityCenters[destIndex];

        return (source, dest);
    }

    private Position SampleInCell(Random random, int cellIndex)
    {
        var center = _cellCenters[cellIndex];
        var lat = center.Latitude + (((random.NextDouble() * 2) - 1) * _halfLat);
        var lon = center.Longitude + (((random.NextDouble() * 2) - 1) * _halfLon);
        return new Position(lon, lat);
    }
}
