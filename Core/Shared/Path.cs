namespace Core.Shared;

public class Path(List<Position> waypoints)
{
    public List<Position> Waypoints { get; } = waypoints;
}
