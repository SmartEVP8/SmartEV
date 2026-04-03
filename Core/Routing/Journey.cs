namespace Core.Routing;

using Core.Shared;
using Core.GeoMath;

/// <summary>
/// The immutable baseline of the journey as originally planned.
/// </summary>
/// <param name="Departure">The time the journey started.</param>
/// <param name="Duration">The original duration of the journey.</param>
/// <param name="DistanceKm">The original distance of the journey in kilometers.</param>
public record OriginalJourney(Time Departure, Time Duration, float DistanceKm)
{
    /// <summary>Gets the original estimated time of arrival.</summary>
    public Time Eta => Departure + Duration;
}

/// <summary>
/// The live state of the journey, updated as rerouting occurs.
/// </summary>
/// <param name="Departure">The time the current leg of the journey started.</param>
/// <param name="Duration">The duration of the current leg of the journey.</param>
/// <param name="DistanceKm">The distance of the current journey in kilometers.</param>
/// <param name="Waypoints">The current waypoints of the journey.</param>
/// <param name="NextStop">The next stop of the journey.</param>
/// <param name="PathDeviation">The accumulated time added by rerouting.</param>
public record CurrentJourney(
    Time Departure,
    Time Duration,
    float DistanceKm,
    List<Position> Waypoints,
    Position NextStop,
    Time PathDeviation)
{
    /// <summary>Gets the current estimated time of arrival.</summary>
    public Time Eta => Departure + Duration;
}

/// <summary>
/// Represents a journey for an electric vehicle.
/// </summary>
/// <param name="departure">The time the journey started.</param>
/// <param name="duration">The original duration of the journey.</param>
/// <param name="distanceMeters">The distance of the journey.</param>
/// <param name="waypoints">The waypoints in the journey.</param>
public class Journey(Time departure, Time duration, float distanceMeters, List<Position> waypoints)
{
    /// <summary>Gets the original baseline of the journey.</summary>
    public OriginalJourney Original { get; } = new(departure, duration, distanceMeters / 1000);

    /// <summary>Gets the live state of the journey.</summary>
    public CurrentJourney Current { get; private set; } = new(
        departure, duration, distanceMeters / 1000,
        waypoints, waypoints[^1], PathDeviation: 0);

    /// <summary>Calculates the time elapsed since the journey started.</summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The elapsed time.</returns>
    public Time TimeElapsed(Time currentTime) => currentTime - Original.Departure;

    /// <summary>
    /// Calculates the remaining time on the current route,
    /// i.e. the time until the EV would reach its destination if it continues on its current segment without any detours.
    /// </summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The remaining time on the current route.</returns>
    public Time RemainingCurrentRoute(Time currentTime) => Current.Eta - currentTime;

    /// <summary>Calculates the distance from the EV's current position to the next stop.</summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>Returns the distance to the next stop in kilometers.</returns>
    public float DistanceToNextStop(Time currentTime)
    {
        var currentPos = GetCurrentPosition(currentTime);
        return (float)GeoMath.EquirectangularDistance(currentPos, Current.NextStop) / 1000;
    }

    /// <summary>
    /// Calculates the EV's current position. Assumes the speed is always the same.
    /// </summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The position of the car.</returns>
    /// <exception cref="ArgumentException">Thrown when the current time is before the journey starts or after it has completed.</exception>
    public Position GetCurrentPosition(Time currentTime)
    {
        DeriveRoute(currentTime);
        return Current.Waypoints[0];
    }

    /// <summary>
    /// Updates the route of the journey and calculates the new path deviation based on the new estimated time of arrival (ETA) compared to the old ETA.
    /// </summary>
    /// <param name="waypoints">The new waypoints of the journey.</param>
    /// <param name="nextStop">The next stop/position of <paramref name="waypoints"/>.</param>
    /// <param name="departure">The time the journey update takes effect.</param>
    /// <param name="duration">The duration of the new journey.</param>
    /// <param name="newDistanceKm">The distance of the new journey in kilometers.</param>
    public void UpdateRoute(List<Position> waypoints, Position nextStop, Time departure, Time duration, float newDistanceKm)
    {
        var deviation = Current.PathDeviation + (departure + duration) - Current.Eta;
        Current = new CurrentJourney(departure, duration, newDistanceKm, waypoints, nextStop, deviation);
    }

    private float PercentageCompleted(Time currentTime) => (currentTime - Current.Departure) / (float)Current.Duration;

    private void DeriveRoute(Time currentTime)
    {
        var percentageCompleted = PercentageCompleted(currentTime);
        var ratio = 1 - percentageCompleted;
        var duration = (uint)Math.Ceiling(ratio * Current.Duration);
        var newDistanceKm = ratio * Current.DistanceKm;
        var waypoints = DeriveNewWaypoints(currentTime);

        if (!waypoints.Any(x => x == Current.NextStop))
            throw new ArgumentException("You called this in an illegal context. We should never derive route beyond the station");

        Current = new CurrentJourney(currentTime, duration, newDistanceKm, waypoints, Current.NextStop, Current.PathDeviation);
    }

    private List<Position> DeriveNewWaypoints(Time currentTime)
    {
        Time completedTime = Current.Departure + Current.Duration;
        if (currentTime > completedTime)
            throw new ArgumentException($"Current time: {currentTime} is after the journey has completed: {completedTime}.");
        if (currentTime < Current.Departure)
            throw new ArgumentException($"Current time: {currentTime} is before the journey has started: {Original.Departure}.");

        var percentageCompleted = PercentageCompleted(currentTime);

        var segments = Current.Waypoints
            .Zip(Current.Waypoints.Skip(1))
            .Select(p =>
                (
                    p.First,
                    p.Second,
                    Length: GeoMath.EquirectangularDistance(p.First, p.Second)
                ))
            .ToList();

        var totalLength = segments.Sum(s => s.Length);
        var distanceTraveled = percentageCompleted * totalLength;

        var distanceCovered = 0.0;

        foreach (var (first, second, length) in segments)
        {
            if (distanceCovered + length >= distanceTraveled)
            {
                var remainingDistance = distanceTraveled - distanceCovered;
                var ratio = remainingDistance / length;
                var latitude = first.Latitude + (ratio * (second.Latitude - first.Latitude));
                var longitude = first.Longitude + (ratio * (second.Longitude - first.Longitude));
                var currentPos = new Position(Longitude: longitude, Latitude: latitude);
                return new List<Position>([currentPos, .. Current.Waypoints.SkipWhile(w => w != second)]);
            }

            distanceCovered += length;
        }

        var last = Current.Waypoints[^1];
        return new List<Position>([last]);
    }
}
