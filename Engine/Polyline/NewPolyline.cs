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
        double radius,
        double latSize,
        double lonSize)
    {
        var radiusInLat = radius / latSize;
        var radiusInLon = radius / lonSize;

        var indexesOfValidStations = new List<ushort>();

        for (var i = 0; i < path.Waypoints.Count - 1; i++)
        {
            var wp = path.Waypoints[i];
            var wp2 = path.Waypoints[i + 1];
            var minPos = new Position(Math.Min(wp.Longitude, wp2.Longitude) - radiusInLon, Math.Min(wp.Latitude, wp2.Latitude) - radiusInLat);
            var maxPos = new Position(Math.Max(wp.Longitude, wp2.Longitude) + radiusInLon, Math.Max(wp.Latitude, wp2.Latitude) + radiusInLat);
            indexesOfValidStations.AddRange(stations.GetStations(minPos, maxPos, wp, wp2, radius));
        }

        return [.. indexesOfValidStations.Distinct()];
    }

}
