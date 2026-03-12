namespace Engine.Polyline;

using Core.Charging;
using Core.Shared;
using Engine.GeoMath;
using Engine.Grid;

public static class NewPolyline
{
    public static List<ushort> StationsInPolyline(
        SpatialGrid stations,
        Paths path,
        double radius) => stations.GetStationsAlongPolyline(path, radius);
}
