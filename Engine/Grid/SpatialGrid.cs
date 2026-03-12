namespace Engine.Grid;

using Core.Charging;
using Core.Shared;
using Engine.GeoMath;

public class SpatialGrid
{
    private readonly Dictionary<RowCol, List<ushort>> _cells = [];
    private readonly Dictionary<ushort, Position> _stationPositions = [];
    private readonly Position _min;
    private readonly double _latSize;
    private readonly double _lonSize;

    public SpatialGrid(SpawnGrid spawnable, IEnumerable<Station> stations)
    {
        _min = spawnable.Min;
        _latSize = spawnable.LatSize;
        _lonSize = spawnable.LonSize;

        foreach (var cell in spawnable.Cells.SelectMany(row => row).Where(c => c.Spawnable))
        {
            var key = ToRowCol(cell.Centerpoint.Latitude, cell.Centerpoint.Longitude);
            _cells[key] = [];
        }

        foreach (var station in stations)
        {
            _stationPositions[station.GetId()] = station.Position;
            var key = ToRowCol(station.Position.Latitude, station.Position.Longitude);
            if (_cells.TryGetValue(key, out var list))
                list.Add(station.GetId());
        }
    }

    /// <summary>
    /// Given a position, return the list of station ids that are in the same cell as that position.
    /// </summary>
    /// <param name="pos">The position of interest.</param>
    /// <returns>A list of uints of station id's.</returns>
    public IReadOnlyList<ushort> GetStations(Position pos)
    {
        var key = ToRowCol(pos.Latitude, pos.Longitude);
        return _cells.TryGetValue(key, out var list) ? list : [];
    }

    /// <summary>
    /// Given two positions representing a bounding box, return the list of stations ids that are within it.
    /// </summary>
    /// <param name="minPos">Left-bottom corner of the bounding box.</param>
    /// <param name="maxPos">Right-top corner of the bounding box.</param>
    /// <returns>A list of uints of station id's.</returns>
    public IReadOnlyList<ushort> GetStations(Position minPos, Position maxPos, Position wp1, Position wp2, double radius)
    {
        var minLat = Math.Min(minPos.Latitude, maxPos.Latitude);
        var maxLat = Math.Max(minPos.Latitude, maxPos.Latitude);
        var minLon = Math.Min(minPos.Longitude, maxPos.Longitude);
        var maxLon = Math.Max(minPos.Longitude, maxPos.Longitude);

        var minRowCol = ToRowCol(minLat, minLon);
        var maxRowCol = ToRowCol(maxLat, maxLon);

        var result = new HashSet<ushort>();

        for (var row = minRowCol.Row; row <= maxRowCol.Row; row++)
        {
            for (var col = minRowCol.Col; col <= maxRowCol.Col; col++)
            {
                var key = new RowCol(row, col);
                if (_cells.TryGetValue(key, out var list))
                {
                    foreach (var stationId in list)
                    {
                        if (_stationPositions.TryGetValue(stationId, out var stationPos))
                        {
                            if (GeoMath.IsInRadius(stationPos, wp1, wp2, radius))
                                result.Add(stationId);
                        }
                    }
                }
            }
        }

        return [.. result];
    }

    private RowCol ToRowCol(double lat, double lon) => new(
        (int)Math.Floor((lat - _min.Latitude) / _latSize),
        (int)Math.Floor((lon - _min.Longitude) / _lonSize)
    );

    public List<ushort> GetStationsAlongPolyline(
    Paths path,
    double radius)
    {
        var midLat = path.Waypoints.Average(w => w.Latitude);
        var latKmPerDeg = 111.32;
        var lonKmPerDeg = 111.32 * Math.Cos(midLat * Math.PI / 180.0);

        var radiusInLatDeg = radius / latKmPerDeg;
        var radiusInLonDeg = radius / lonKmPerDeg;

        var seen = new HashSet<ushort>();

        for (var i = 0; i < path.Waypoints.Count - 1; i++)
        {
            var wp = path.Waypoints[i];
            var wp2 = path.Waypoints[i + 1];
            var minPos = new Position(
                Math.Min(wp.Longitude, wp2.Longitude) - radiusInLonDeg,
                Math.Min(wp.Latitude, wp2.Latitude) - radiusInLatDeg);
            var maxPos = new Position(
                Math.Max(wp.Longitude, wp2.Longitude) + radiusInLonDeg,
                Math.Max(wp.Latitude, wp2.Latitude) + radiusInLatDeg);

            CollectSegment(minPos, maxPos, wp, wp2, radius, seen);
        }

        return [.. seen];
    }

    private void CollectSegment(
        Position minPos, Position maxPos,
        Position wp1, Position wp2,
        double radius,
        HashSet<ushort> result)
    {
        var minRowCol = ToRowCol(minPos.Latitude, minPos.Longitude);
        var maxRowCol = ToRowCol(maxPos.Latitude, maxPos.Longitude);

        for (var row = minRowCol.Row; row <= maxRowCol.Row; row++)
            for (var col = minRowCol.Col; col <= maxRowCol.Col; col++)
                if (_cells.TryGetValue(new RowCol(row, col), out var list))
                    foreach (var stationId in list)
                        if (!result.Contains(stationId))
                            if (_stationPositions.TryGetValue(stationId, out var pos))
                                if (GeoMath.IsInRadius(pos, wp1, wp2, radius))
                                    result.Add(stationId);
    }
}

readonly struct RowCol(int row, int col)
{
    public int Row { get; } = row;
    public int Col { get; } = col;
}
