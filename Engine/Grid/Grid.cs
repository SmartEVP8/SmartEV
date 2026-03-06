namespace Engine.Grid;

using Core.Shared;

/// <summary>
/// A grid of spawnable cells that are either one or zero
/// </summary>
/// <param name="spawnableCells">The spawnable cell and their midpoint.</param>
public class Grid(GridCell[][] spawnableCells)
{
    /// <summary>
    /// A 2D array of GridCells, where each cell contains a boolean indicating if it's spawnable and its midpoint position.
    /// </summary>
    public readonly GridCell[][] SpawnableCells = spawnableCells;
}

/// <summary>
/// A single cell in the grid, which can be spawnable or not, and has a midpoint position
/// </summary>
/// <param name="spawnable">Bool for spawnable or now.</param>
/// <param name="midpoint">Center of the grid.</param>
public class GridCell(bool spawnable, Position midpoint)
{
    /// <summary>
    /// Spawnable indicates whether this cell is spawnable (true) or not (false).
    /// </summary>
    public bool Spawnable = spawnable;

    /// <summary>
    /// The midpoint of the cell, represented as a Position (latitude and longitude).
    /// </summary>
    public Position Midpoint = midpoint;
}
