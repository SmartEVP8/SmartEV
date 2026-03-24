namespace Engine.Grid;

using Core.Shared;

/// <summary>
/// A grid of spawnable cells that are either one or zero.
/// </summary>
/// <param name="spawnableCells">The spawnable cell and their midpoint.</param>
/// <param name="min">The minimum latitude and longitude of the grid, representing the bottom-left corner of the grid.</param>
/// <param name="latSize">The size of each cell in terms of latitude.</param>
/// <param name="lonSize">The size of each cell in terms of longitude.</param>
public class SpawnGrid(List<List<GridCell>> spawnableCells, Position min, double latSize, double lonSize)
{
    /// <summary>Gets the 2D list of grid cells.</summary>
    public List<List<GridCell>> Cells { get; } = spawnableCells;

    /// <summary>
    /// Gets the minimum latitude and longitude of the grid, representing the bottom-left corner of the grid.
    /// </summary>
    public Position Min { get; } = min;

    /// <summary>
    /// Gets the size of each cell in terms of latitude and longitude.
    /// </summary>
    public double LatSize { get; } = latSize;

    /// <summary>
    /// Gets the size of each cell in terms of longitude.
    /// </summary>
    public double LonSize { get; } = lonSize;

    /// <summary>
    /// Given a position, compute the corresponding cell in the grid by calculating the row and column indices based on the minimum latitude/longitude and the size of each cell.
    /// </summary>
    /// <param name="position">The position for which to find the corresponding cell.</param>
    /// <returns>The corresponding GridCell, or null if the position is outside the grid.</returns>
    public GridCell? GetCell(Position position)
    {
        var row = (int)((position.Latitude - Min.Latitude) / LatSize);
        var col = (int)((position.Longitude - Min.Longitude) / LonSize);

        if (row < 0 || row >= Cells.Count) return null;
        if (col < 0 || col >= Cells[row].Count) return null;

        return Cells[row][col];
    }

    /// <summary>Gets all spawnable cells in the grid.</summary>
    /// <returns>An enumerable of all spawnable GridCells in the grid.</returns>
    public IEnumerable<GridCell> GetSpawnableCells() =>
        Cells.SelectMany(row => row).Where(c => c.Spawnable);

    /// <summary>
    /// Given a cell, compute the bounding box (min and max lat/lon) of that cell based on its center point and the grid's lat/lon size.
    /// </summary>
    /// <param name="cell">The cell for which to compute the bounding box.</param>
    /// <returns>
    /// A tuple containing the minimum and maximum Position (lat/lon) that defines the bounding box of the cell.
    /// </returns>
    public (Position Min, Position Max) GetBoundingBox(GridCell cell)
    {
        var halfLat = LatSize / 2.0;
        var halfLon = LonSize / 2.0;

        var min = new Position(
            cell.Centerpoint.Longitude - halfLon,
            cell.Centerpoint.Latitude - halfLat);

        var max = new Position(
            cell.Centerpoint.Longitude + halfLon,
            cell.Centerpoint.Latitude + halfLat);

        return (min, max);
    }
}

/// <summary>
/// A single cell in the grid, which can be spawnable or not, and has a midpoint position.
/// </summary>
/// <param name="spawnable">Bool for spawnable or now.</param>
/// <param name="centerpoint">Center of the grid.</param>
public class GridCell(bool spawnable, Position centerpoint)
{
    /// <summary>Gets a value indicating whether this cell is spawnable.</summary>
    public bool Spawnable { get; } = spawnable;

    /// <summary>Gets the centerpoint of the cell.</summary>
    public Position Centerpoint { get; } = centerpoint;
}
