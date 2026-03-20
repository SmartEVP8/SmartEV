namespace Engine.Routing;

using Core.Routing;
using Core.Shared;

public class PathDeviator(OSRMRouter osrmRouter)
{
    private readonly OSRMRouter _osrmRouter = osrmRouter;

    /// <summary>
    /// Calculates the deviation from the current position to the end of the journey, detouring through a station.
    /// This is the "current position" deviation, which is the duration of the new route from the current position to the end, minus the remaining time of the original route.
    /// </summary>
    /// <param name="journey">The original journey.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="stationPosition">The position of the station to detour to.</param>
    /// <returns>The deviation.</returns>
    public (float, string) CalculateCurrentPositionDeviation(Journey journey, Time currentTime, Position stationPosition)
    {
        var currentPosition = journey.CurrentPosition(currentTime);
        var destination = journey.Path.Waypoints.Last();

        static (double, double) ToCoord(Position p) => (p.Longitude, p.Latitude);

        (double, double)[] routeThroughStation =
        [
            ToCoord(currentPosition),
            ToCoord(stationPosition),
            ToCoord(destination),
        ];

        var originalRemainingTime = (int)journey.OriginalDuration - (int)journey.TimeElapsed(currentTime);
        var (detourDuration, polyline) = _osrmRouter.QueryDestination(routeThroughStation);
        var detourDeviation = detourDuration - originalRemainingTime;

        return (detourDeviation, polyline);
    }

    /// <summary>
    /// Adds the deviation to the sum of deviations for the journey.
    /// </summary>
    /// <param name="journey">The journey to update.</param>
    /// <param name="deviation">The deviation to add.</param>
    public void UpdateRunningSumDeviation(Journey journey, float deviation) => journey.RunningSumDeviation += deviation;
}
