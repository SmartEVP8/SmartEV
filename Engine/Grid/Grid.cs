namespace Engine.Grid;

using Core.Shared;

/// <summary>
/// A grid of spawnable cells that are either one or zero
/// </summary>
/// <param name="spawnableCells">The spawnable cell and their midpoint.</param>
public class SpawnGrid(List<List<GridCell>> spawnableCells, Position min, double latSize, double lonSize)
{
    /// <summary>
    /// A 2D array of GridCells, where each cell contains a boolean indicating if it's spawnable and its midpoint position.
    /// </summary>
    public readonly List<List<GridCell>> Cells = spawnableCells;

    public Position Min { get; } = min;

    public double LatSize { get; } = latSize;

    public double LonSize { get; } = lonSize;

    public GridCell? GetCell(Position position)
    {
        var row = (int)((position.Latitude - Min.Latitude) / LatSize);
        var col = (int)((position.Longitude - Min.Longitude) / LonSize);

        if (row < 0 || row >= Cells.Count) return null;
        if (col < 0 || col >= Cells[row].Count) return null;

        return Cells[row][col];
    }

    /// <summary>
    /// Given a cell, compute the bounding box (min and max lat/lon) of that cell based on its center point and the grid's lat/lon size.
    /// </summary>
    /// <param name="cell"></param>
    /// <returns>
    /// A tuple containing the minimum and maximum Position (lat/lon) that defines the bounding box of the cell.
    /// </returns>
    public (Position Min, Position Max) GetBoundingBox(GridCell cell)
    {
        var halfLat = LatSize / 2.0;
        var halfLon = LonSize / 2.0;

        var min = new Position(
            cell.Centerpoint.Longitude - halfLon,
            cell.Centerpoint.Latitude - halfLat
        );

        var max = new Position(
            cell.Centerpoint.Longitude + halfLon,
            cell.Centerpoint.Latitude + halfLat
        );

        return (min, max);
    }
}

/// <summary>
/// A single cell in the grid, which can be spawnable or not, and has a midpoint position
/// </summary>
/// <param name="spawnable">Bool for spawnable or now.</param>
/// <param name="centerpoint">Center of the grid.</param>
public class GridCell(bool spawnable, Position centerpoint)
{
    /// <summary>
    /// Spawnable indicates whether this cell is spawnable (true) or not (false).
    /// </summary>
    public bool Spawnable = spawnable;

    /// <summary>
    /// The midpoint of the cell, represented as a Position (latitude and longitude).
    /// </summary>
    public Position Centerpoint = centerpoint;
}
