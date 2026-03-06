namespace Core.Shared;

public class Paths(List<Position> waypoints)
{
    public List<Position> Waypoints { get; } = waypoints;
}
