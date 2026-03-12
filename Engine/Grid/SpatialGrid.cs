namespace Engine.Grid;

using Core.Charging;
using Core.Shared;

public class SpatialGrid
{
    private readonly Dictionary<RowCol, List<uint>> _cells = [];
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
            var key = ToRowCol(station.Position.Latitude, station.Position.Longitude);
            if (_cells.TryGetValue(key, out var list))
            {
                list.Add(station.GetId());
            }
            else
            {
                throw new Exception($"Station {station.GetId()} at position {station.Position} is outside the grid bounds.");
            }
        }
    }

    /// <summary>
    /// Given a position, return the list of station ids that are in the same cell as that position.
    /// </summary>
    /// <param name="pos">The position of interest.</param>
    /// <returns>A list of uints of station id's.</returns>
    public IReadOnlyList<uint> GetStations(Position pos)
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
    public IReadOnlyList<uint> GetStations(Position minPos, Position maxPos)
    {
        var minLat = Math.Min(minPos.Latitude, maxPos.Latitude);
        var maxLat = Math.Max(minPos.Latitude, maxPos.Latitude);
        var minLon = Math.Min(minPos.Longitude, maxPos.Longitude);
        var maxLon = Math.Max(minPos.Longitude, maxPos.Longitude);

        var minRowCol = ToRowCol(minLat, minLon);
        var maxRowCol = ToRowCol(maxLat, maxLon);

        var result = new HashSet<uint>();

        for (var row = minRowCol.Row; row <= maxRowCol.Row; row++)
        {
            for (var col = minRowCol.Col; col <= maxRowCol.Col; col++)
            {
                var key = new RowCol(row, col);
                if (_cells.TryGetValue(key, out var list))
                {
                    foreach (var stationId in list)
                        _ = result.Add(stationId);
                }
            }
        }

        return [.. result];
    }

    private RowCol ToRowCol(double lat, double lon) => new(
        (int)Math.Floor((lat - _min.Latitude) / _latSize),
        (int)Math.Floor((lon - _min.Longitude) / _lonSize)
    );
}

readonly struct RowCol(int row, int col)
{
    public int Row { get; } = row;
    public int Col { get; } = col;
}
