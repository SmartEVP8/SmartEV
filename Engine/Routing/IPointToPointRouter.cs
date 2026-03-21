namespace Engine.Routing;

public interface IPointToPointRouter
{
    (float duration, string polyline) QuerySingleDestination(double evLon, double evLat, double destLon, double destLat);
}
