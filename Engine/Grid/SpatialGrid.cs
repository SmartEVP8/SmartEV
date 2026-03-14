namespace Engine.Grid;

using Core.Charging;
using Core.Shared;
using Engine.GeoMath;

/// <summary>
/// The SpatialGrid class is a spatial index that allows for efficient querying of stations based on their geographic location.
/// </summary>
public class SpatialGrid
{
    private readonly Dictionary<RowCol, List<ushort>> _cells = [];
    private readonly Dictionary<ushort, Position> _stationPositions = [];
    private readonly Position _min;
    private readonly double _latSize;
    private readonly double _lonSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpatialGrid"/> class.
    /// Initializes the spatial grid with the given spawnable grid and stations.
    /// The spawnable grid defines the bounds and cell sizes of the spatial grid, while the stations are used to populate the cells with station ids.
    /// </summary>
    /// <param name="spawnable">The spawnable grid defines the bounds and cell sizes of the spatial grid. It is used to determine which cells are spawnable and to initialize the grid structure.</param>
    /// <param name="stations">Used as points to be queried for.</param>
    /// <exception cref="Exception">Thrown if a station is located outside the bounds of the grid defined by the spawnable parameter.</exception>
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
            {
                list.Add(station.GetId());
            }
            else
            {
                throw new Exception($"Station {station.GetId()} at position {station.Position.Latitude}, {station.Position.Longitude} is outside the grid bounds.");
            }
        }
    }

    /// <summary>
    /// Given a polyline (a list of waypoints) and a radius, return the list of station ids that are within the radius of any point along the polyline.
    /// </summary>
    /// <param name="path">The polyline / list of waypoints.</param>
    /// <param name="radius">Radius to search around the polyline.</param>
    /// <returns>A lits of uints of stations id's.</returns>
    public List<ushort> GetStationsAlongPolyline(
    Paths path,
    double radius)
    {
        var radiusInLatDeg = radius / GeoMath.KmPerLatitudeDegree;
        var radiusInLonDeg = radius / GeoMath.KmPerLongtitudeDegree;
        var seen = new HashSet<ushort>();

        for (var i = 0; i < path.Waypoints.Count - 1; i++)
        {
            var wp1 = path.Waypoints[i];
            var wp2 = path.Waypoints[i + 1];
            var minPos = new Position(
                Math.Min(wp1.Longitude, wp2.Longitude) - radiusInLonDeg,
                Math.Min(wp1.Latitude, wp2.Latitude) - radiusInLatDeg);
            var maxPos = new Position(
                Math.Max(wp1.Longitude, wp2.Longitude) + radiusInLonDeg,
                Math.Max(wp1.Latitude, wp2.Latitude) + radiusInLatDeg);

            CollectSegment(minPos, maxPos, wp1, wp2, radius, seen);
        }

        return [.. seen];
    }

    /// <summary>
    /// Converts a latitude and longitude to a row and column index in the grid.
    /// </summary>
    private RowCol ToRowCol(double lat, double lon) => new(
            (int)Math.Floor((lat - _min.Latitude) / _latSize),
            (int)Math.Floor((lon - _min.Longitude) / _lonSize)
        );

    /// <summary>
    /// Collects station ids for stations that are within the radius of the line segment defined by wp1 and wp2.
    /// </summary>
    private void CollectSegment(
        Position minPos,
        Position maxPos,
        Position wp1,
        Position wp2,
        double radius,
        HashSet<ushort> result)
    {
        var minRowCol = ToRowCol(minPos.Latitude, minPos.Longitude);
        var maxRowCol = ToRowCol(maxPos.Latitude, maxPos.Longitude);

        for (var row = minRowCol.Row; row <= maxRowCol.Row; row++)
        {
            for (var col = minRowCol.Col; col <= maxRowCol.Col; col++)
            {
                if (!_cells.TryGetValue(new RowCol(row, col), out var list))
                    continue;

                foreach (var stationId in list)
                {
                    if (result.Contains(stationId))
                        continue;

                    if (!_stationPositions.TryGetValue(stationId, out var pos))
                        continue;

                    if (GeoMath.IsInRadius(pos, wp1, wp2, radius))
                        result.Add(stationId);
                }
            }
        }
    }

    private readonly struct RowCol(int row, int col)
    {
        public int Row { get; } = row;

        public int Col { get; } = col;
    }
}
