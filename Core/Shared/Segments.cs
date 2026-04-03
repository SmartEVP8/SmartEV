namespace Core.Shared;

/// <summary>
/// A simple wrapper for a list of waypoints.
/// </summary>
public class Segments(List<Position> waypoints)
{
    /// <summary>
    /// Gets the list of waypoints that make up the all segments. Each waypoint is a Position.
    /// </summary>
    public List<Position> Waypoints { get; } = waypoints;
}
