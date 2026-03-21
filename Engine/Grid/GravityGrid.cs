namespace Engine.Grid;

using Core.Shared;

internal class GravityGrid(List<List<GravityCell>> cells, Position[] cityCenters, double halfLat, double halfLon)
{
    public readonly List<List<GravityCell>> Cells = cells;
    public readonly double HalfLat = halfLat;
    public readonly double HalfLon = halfLon;
    public readonly Position[] CityCenters = cityCenters;
    public readonly Position[] CellCenters = [.. cells
            .SelectMany(g => g)
            .Select(c => c.Centerpoint)];
}

internal class GravityCell(Position centerPoint, List<CityInfo> cityInfo)
{
    public readonly Position Centerpoint = centerPoint;
    public readonly List<CityInfo> CityInfo = cityInfo;
}

internal readonly struct CityInfo(string cityName, float distToCity, float population)
{
    public readonly string CityName = cityName;
    public readonly float DistToCity = distToCity;
    public readonly float Population = population;
}
