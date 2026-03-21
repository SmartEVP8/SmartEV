namespace Engine.Grid;

using Core.Shared;
using Engine.Spawning;

public interface IJourneySampler
{
    (Position Source, Position Destination) SampleSourceToDest(Random random);
}

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
