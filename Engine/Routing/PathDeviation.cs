namespace Engine.Routing;

using Core.Routing;
using Core.Shared;

public class PathDeviation(OSRMRouter osrmRouter)
{
    private OSRMRouter _osrmRouter = osrmRouter;

    public Tuple<Time, float> CalculateRunningSumDeviation(Journey journey)
    {
        var totalDuration = journey.Duration;
        var originalRouteDuration = journey.Duration; // TODO: should be the duration of the original route, not the current route, but we can assume they are the same for now since we are only testing with one route.

        var deviationDuration = totalDuration - originalRouteDuration;
        var percentageDeviation = deviationDuration / originalRouteDuration;
        return new Tuple<Time, float>(new Time(deviationDuration), percentageDeviation);
    }

    public Tuple<Time, float> CalculateCurrentPositionDeviation(Journey journey, Time currentTime, Position detourDestination)
    {
        var currentPosition = journey.CurrentPosition(currentTime);
        var currentPositionCoords = Tuple.Create(currentPosition.Longitude, currentPosition.Latitude);
        var detourPositionCoords = Tuple.Create(detourDestination.Longitude, detourDestination.Latitude);
        var destinationCoords = Tuple.Create(journey.Path.Waypoints.Last().Longitude, journey.Path.Waypoints.Last().Latitude);

        var timeElapsed = currentTime - journey.Departure;
        var remainingTime = journey.Duration - timeElapsed;

        // Maybe pass as parameter to avoid calling multiple times.
        var detourRoute = _osrmRouter.QueryDestination([currentPositionCoords, detourPositionCoords, destinationCoords]);

        var deviationDuration = detourRoute.duration - remainingTime;
        var deviationPercent = deviationDuration / remainingTime * 100f;

        return new Tuple<Time, float>(new Time((int)deviationDuration), deviationPercent);
    }
}