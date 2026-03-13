using Core.Shared;
using Core.Spawning;

internal class GravityGrid(List<List<GravityCell>> cells)
{
    public readonly List<List<GravityCell>> Cells = cells;
}

internal class GravityCell(Position centerPoint, List<CityInfo> cityInfo)
{
    public readonly Position Centerpoint = centerPoint;
    public readonly List<CityInfo> CityInfo = cityInfo;
}

internal struct CityInfo(string cityName, float distToCity, float population)
{
    public readonly string CityName = cityName;
    public readonly float DistToCity = distToCity;
    public readonly float Population = population;
}

public class SimulationSamplers(AliasSampler source, AliasSampler[] destinations)
{
    public readonly AliasSampler SourceSampler = source;
    public readonly AliasSampler[] DestinationSamplers = destinations;
}
