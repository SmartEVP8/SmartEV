namespace Engine.Grid;

using Core.Shared;
using Engine.Spawning;
using Core.GeoMath;

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
/// <param name="wetPolygons">List of wet polygons used to ensure that sampled positions do not fall within wet areas.</param>
public class JourneySamplers(
    AliasSampler source,
    AliasSampler[] destinations,
    Position[] cellCenters,
    Position[] cityCenters,
    double halfLat,
    double halfLon,
    List<List<Position>> wetPolygons) : IJourneySampler
{
    private record PolygonWithBounds(List<Position> Polygon, double MinLat, double MaxLat, double MinLon, double MaxLon);

    private readonly AliasSampler _sourceSampler = source;
    private readonly AliasSampler[] _destinationSamplers = destinations;
    private readonly Position[] _cityCenters = cityCenters;
    private readonly Position[] _cellCenters = cellCenters;
    private readonly double _halfLat = halfLat;
    private readonly double _halfLon = halfLon;
    private readonly List<PolygonWithBounds> _wetPolygons = [.. wetPolygons
        .Select(p => new PolygonWithBounds(
            p,
            p.Min(v => v.Latitude),
            p.Max(v => v.Latitude),
            p.Min(v => v.Longitude),
            p.Max(v => v.Longitude)))];

    /// <summary>
    /// Samples a source and destination position for a journey.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <returns>A tuple containing the source and destination positions.</returns>
    public (Position Source, Position Destination) SampleSourceToDest(Random random)
    {
        var distance = 0.0;
        var dest = new Position(0, 0);
        var source = new Position(0, 0);
        while (distance < 0.5)
        {
            var sourceIndex = _sourceSampler.Sample(random);
            source = SampleInCell(random, sourceIndex);

            // Skip if wet — bounds check first, ray cast only if bounds overlap
            if (_wetPolygons.Any(p =>
                source.Longitude >= p.MinLon && source.Longitude <= p.MaxLon &&
                source.Latitude >= p.MinLat && source.Latitude <= p.MaxLat &&
                PointInPolygon(p.Polygon, source.Longitude, source.Latitude)))

                // TODO: Add logger here when Bech things are ready to check how many times this happens and whether it's a problem.
                continue;

            var destIndex = _destinationSamplers[sourceIndex].Sample(random);
            dest = _cityCenters[destIndex];
            distance = GeoMath.EquirectangularDistance(source, dest);
        }

        return (source, dest);
    }

    private Position SampleInCell(Random random, int cellIndex)
    {
        var center = _cellCenters[cellIndex];
        var lat = center.Latitude + (((random.NextDouble() * 2) - 1) * (_halfLat / 2));
        var lon = center.Longitude + (((random.NextDouble() * 2) - 1) * (_halfLon / 2));
        return new Position(lon, lat);
    }

    private static bool PointInPolygon(List<Position> polygon, double lon, double lat)
    {
        var inside = false;
        var vertexCount = polygon.Count;

        for (var current = 0; current < vertexCount; current++)
        {
            var previous = (current + vertexCount - 1) % vertexCount;

            var currentLon = polygon[current].Longitude;
            var currentLat = polygon[current].Latitude;
            var previousLon = polygon[previous].Longitude;
            var previousLat = polygon[previous].Latitude;

            if ((currentLat > lat) == (previousLat > lat))
                continue;

            var interpolationFactor = (lat - currentLat) / (previousLat - currentLat);
            var crossingLon = currentLon + ((previousLon - currentLon) * interpolationFactor);

            if (lon < crossingLon)
                inside = !inside;
        }

        return inside;
    }
}