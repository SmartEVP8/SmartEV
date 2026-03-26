namespace Core.Shared;

/// <summary>
/// A simple wrapper for a list of waypoints, representing a path.
/// </summary>
public class Paths(List<Position> waypoints)
{
    /// <summary>
    /// Gets the list of waypoints that make up the path. Each waypoint is a Position.
    /// </summary>
    public List<Position> Waypoints { get; } = waypoints;
}
