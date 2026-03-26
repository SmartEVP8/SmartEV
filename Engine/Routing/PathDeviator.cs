namespace Engine.Routing;

using Core.Vehicles;

/// <summary>
/// Calculates detour deviations by querying OSRM routes.
/// </summary>
public static class PathDeviator
{
    /// <summary>
    /// Calculates the extra time added to a journey by detouring through a station,
    /// relative to the original remaining journey time.
    /// </summary>
    /// <param name="ev" >The EV for which to calculate the detour deviation.</param>
    /// <param name="journey">The original journey.</param>
    /// <returns>The deviation and detoured route.</returns>
    public static float CalculateDetourDeviation(ref EV ev, (float duration, string polyline) journey)
    {
        var detourDeviation = MathF.Max(0, journey.duration - ev.Journey.OriginalDuration);
        return detourDeviation;
    }
}
