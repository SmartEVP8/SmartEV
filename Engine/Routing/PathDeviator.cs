namespace Engine.Routing;

using Core.Routing;
using Core.Shared;

/// <summary>
/// Calculates detour deviations by querying OSRM routes.
/// </summary>
public class PathDeviator(IDestinationRouter osrmRouter)
{
    private readonly IDestinationRouter _osrmRouter = osrmRouter;

    /// <summary>
    /// Calculates the extra time added to a journey by detouring through a station,
    /// relative to the original remaining journey time. Clamped to 0 for use in cost functions.
    /// </summary>
    /// <param name="journey">The original journey.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="stationPosition">The position of the station to detour to.</param>
    /// <returns>The deviation and detoured route.</returns>
    public (float, string) CalculateDetourDeviation(Journey journey, Time currentTime, Position stationPosition)
    {
        var currentPosition = journey.CurrentPosition(currentTime);
        var destination = journey.Path.Waypoints.Last();

        double[] routeThroughStation =
        [
            currentPosition.Longitude,
            currentPosition.Latitude,
            stationPosition.Longitude,
            stationPosition.Latitude,
            destination.Longitude,
            destination.Latitude,
        ];

        var (detourDuration, polyline) = _osrmRouter.QueryDestination(routeThroughStation);

        if (detourDuration < 0)
            throw new InvalidOperationException("Failed to calculate detour duration. OSRM returned a negative value.");

        var originalRemainingSeconds = journey.OriginalDuration - journey.TimeElapsed(currentTime);
        var detourDeviation = MathF.Max(0, detourDuration - originalRemainingSeconds);

        return (detourDeviation, polyline);
    }
}
