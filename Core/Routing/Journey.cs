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
/// <param name="DurationToNextStop">The duration from departure to the configured next stop.</param>
public record CurrentJourney(
    Time Departure,
    Time Duration,
    float DistanceKm,
    List<Position> Waypoints,
    Position NextStop,
    Time PathDeviation,
    Time DurationToNextStop)
{
    /// <summary>Gets the current estimated time of arrival.</summary>
    public Time Eta => Departure + Duration;

    /// <summary>Gets the estimated time of arrival for the configured next stop.</summary>
    public Time EtaToNextStop => Departure + DurationToNextStop;
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
    public OriginalJourney Original { get; } = new(departure, duration > 0 ? duration : throw new ArgumentOutOfRangeException("Duration Can be zero"), distanceMeters / 1000);

    /// <summary>Gets the live state of the journey.</summary>
    public CurrentJourney Current { get; private set; } = new(
        departure, duration, distanceMeters / 1000,
        waypoints, waypoints[^1], PathDeviation: 0, DurationToNextStop: duration);

    /// <summary>
    /// Calculates the remaining time on the current route,
    /// i.e. the time until the EV would reach its destination if it continues on its current segment without any detours.
    /// </summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The remaining time on the current route.</returns>
    public Time RemainingCurrentRoute(Time currentTime) => Current.Eta - currentTime;

    /// <summary>
    /// Calculates the remaining distance to destination at <paramref name="currentTime"/> without mutating the live journey.
    /// </summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The remaining distance to destination in kilometers.</returns>
    public float RemainingDistanceToDestination(Time currentTime)
    {
        CheckIfTimeIsBeforeDeparture(currentTime);
        CheckIfTimeIsAfterCompletion(currentTime);
        var (snapshot, _) = DeriveRouteSnapshot(currentTime);
        return Math.Max(0f, snapshot.DistanceKm);
    }

    /// <summary>
    /// Calculates the EV's current position without mutating the live route.
    /// </summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The position of the car.</returns>
    /// <exception cref="ArgumentException">Thrown when the current time is before the journey starts or after it has completed.</exception>
    public Position GetCurrentPosition(Time currentTime)
    {
        CheckIfTimeIsBeforeDeparture(currentTime);
        CheckIfTimeIsAfterCompletion(currentTime);
        var (_, currentPos) = DeriveRouteSnapshot(currentTime);
        return currentPos;
    }

    /// <summary>
    /// Advances the live route to <paramref name="currentTime"/> and returns the current position.
    /// </summary>
    /// <param name="currentTime">The simulation time to advance to.</param>
    /// <returns>The position of the car after advancing.</returns>
    public Position AdvanceTo(Time currentTime)
    {
        CheckIfTimeIsBeforeDeparture(currentTime);
        CheckIfTimeIsAfterEtaToNextStop(currentTime);
        CheckIfTimeIsAfterCompletion(currentTime);
        var (currentJourney, currentPos) = DeriveRouteSnapshot(currentTime);
        Current = currentJourney;
        return currentPos;
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
        CheckIfTimeIsBeforeDeparture(departure);
        var deviation = Current.PathDeviation + (departure + duration) - Current.Eta;
        var durationToNextStop = DurationToNextStop(duration, waypoints, nextStop);
        Current = new CurrentJourney(departure, duration, newDistanceKm, waypoints, nextStop, deviation, durationToNextStop);
    }

    /// <summary>
    /// Updates the current journey to have an EV drive towards its destination after charging at a station.
    /// </summary>
    /// <param name="timeAtStation">The amount of time spent at a station.</param>
    public void UpdateRouteToDestination(Time timeAtStation)
    {

        var newDeparture = Current.Departure + timeAtStation;

        Current = new CurrentJourney(
            Departure: newDeparture,
            Duration: Current.Duration,
            DistanceKm: Current.DistanceKm,
            Waypoints: Current.Waypoints,
            NextStop: Current.Waypoints.Last(),
            PathDeviation: Current.PathDeviation,
            DurationToNextStop: DurationToNextStop(Current.Duration, Current.Waypoints, Current.Waypoints.Last()));
    }

    /// <summary>
    /// Finds the time it would take to drive a given distance based on the original average speed of the journey.
    /// </summary>
    /// <param name="distance">The distance a EV has to drive.</param>
    /// <returns>Returns how long it takes to drive a distance in seconds.</returns>
    public Time TimeToDriveDistance(float distance)
    {
        var speedKmh = Original.DistanceKm / (Original.Duration.Seconds / 3600f);
        var timeHours = distance / speedKmh;
        return (uint)Math.Ceiling(timeHours * 3600);
    }

    private float PercentageCompleted(Time currentTime)
    {
        if (Current.Duration.Seconds == 0)
            return 1f;

        var progress = (currentTime - Current.Departure) / (float)Current.Duration;
        return Math.Clamp(progress, 0f, 1f);
    }

    /// <summary>
    /// Derives a new journey snapshot at <paramref name="currentTime"/> by trimming elapsed route and
    /// recomputing durations/distances from the interpolated current position.
    /// </summary>
    /// <param name="currentTime">The simulation time to derive state for.</param>
    /// <returns>A new route snapshot together with the interpolated current position.</returns>
    private (CurrentJourney Journey, Position CurrentPosition) DeriveRouteSnapshot(Time currentTime)
    {
        var percentageCompleted = PercentageCompleted(currentTime);
        var ratio = 1 - percentageCompleted;
        var duration = (uint)Math.Ceiling(ratio * Current.Duration);
        var newDistanceKm = ratio * Current.DistanceKm;
        var waypoints = DeriveNewWaypoints(currentTime);
        var durationToNextStop = DurationToNextStop(duration, waypoints, Current.NextStop);

        var currentJourney = new CurrentJourney(currentTime, duration, newDistanceKm, waypoints, Current.NextStop, Current.PathDeviation, durationToNextStop);
        return (currentJourney, waypoints[0]);
    }

    /// <summary>
    /// Derives the route tail from <paramref name="currentTime"/>.
    /// </summary>
    /// <param name="currentTime">Simulation time to derive from.</param>
    /// <returns>
    /// A list that starts with the interpolated current position, followed by the remaining waypoints
    /// from the interpolation segment boundary. The configured <see cref="CurrentJourney.NextStop"/>
    /// is preserved as a hard boundary for derivation, even if the returned suffix contains waypoints beyond it.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="currentTime"/> is outside valid bounds or if derivation would move
    /// beyond <see cref="CurrentJourney.NextStop"/>.
    /// </exception>
    private List<Position> DeriveNewWaypoints(Time currentTime)
    {
        CheckIfTimeIsAfterCompletion(currentTime);
        CheckIfTimeIsAfterEtaToNextStop(currentTime);
        CheckIfTimeIsBeforeDeparture(currentTime);

        var percentageCompleted = PercentageCompleted(currentTime);

        var segments = Enumerable.Range(0, Current.Waypoints.Count - 1)
            .Select(i =>
                (
                    First: Current.Waypoints[i],
                    Second: Current.Waypoints[i + 1],
                    SecondIndex: i + 1,
                    Length: GeoMath.EquirectangularDistance(Current.Waypoints[i], Current.Waypoints[i + 1])
                ))
            .ToList();

        var totalLength = segments.Sum(s => s.Length);
        var distanceTraveled = percentageCompleted * totalLength;

        var distanceCovered = 0.0;

        foreach (var (first, second, secondIndex, length) in segments)
        {
            if (distanceCovered + length < distanceTraveled)
            {
                distanceCovered += length;
                continue;
            }

            var remainingDistance = distanceTraveled - distanceCovered;
            var ratio = remainingDistance / length;
            var latitude = first.Latitude + (ratio * (second.Latitude - first.Latitude));
            var longitude = first.Longitude + (ratio * (second.Longitude - first.Longitude));
            var currentPos = new Position(Longitude: longitude, Latitude: latitude);

            var nextStopIndex = FindNextStopIndex(Current.Waypoints, Current.NextStop);
            var nextStopExists = nextStopIndex >= 0;
            var interpolationPassedNextStop = nextStopExists && secondIndex > nextStopIndex;
            if (interpolationPassedNextStop)
            {
                if (!currentTime.IsApproximately(Current.EtaToNextStop))
                    throw new ArgumentException($"Illegal context: interpolation moved beyond next stop at currentTime={currentTime}. nextStopIndex={nextStopIndex}, segmentIndex={secondIndex}.");

                // Rounding nudged us one segment past the next stop — snap back to it.
                var nextStopPos = Current.Waypoints[nextStopIndex];
                var remainingWaypoints = Current.Waypoints.Skip(nextStopIndex + 1).ToList();
                return new List<Position>([nextStopPos, .. remainingWaypoints]);
            }

            var suffixStartIndex = secondIndex;
            var remainingWaypoints2 = Current.Waypoints.Skip(suffixStartIndex).ToList();

            return new List<Position>([currentPos, .. remainingWaypoints2]);
        }

        var last = Current.Waypoints[^1];
        return new List<Position>([last]);
    }

    /// <summary>
    /// Estimates the duration from departure to <paramref name="nextStop"/> by proportional path length,
    /// assuming constant speed along the current waypoint polyline.
    /// </summary>
    /// <param name="totalDuration">Duration of the full route represented by <paramref name="waypoints"/>.</param>
    /// <param name="waypoints">Ordered route waypoints.</param>
    /// <param name="nextStop">Configured next stop on the route.</param>
    /// <returns>Estimated duration from route start to next stop.</returns>
    private static Time DurationToNextStop(Time totalDuration, List<Position> waypoints, Position nextStop)
    {
        var nextStopIndex = FindNextStopIndex(waypoints, nextStop);
        if (nextStopIndex < 0)
            return totalDuration;
        if (nextStopIndex == 0)
            return 0;

        var totalLength = PathLength(waypoints, waypoints.Count - 1);
        if (totalLength <= 0)
            return 0;

        var lengthToNextStop = PathLength(waypoints, nextStopIndex);
        var ratio = lengthToNextStop / totalLength;
        return (uint)Math.Ceiling(ratio * totalDuration);
    }

    /// <summary>
    /// Resolves the effective next-stop waypoint index on a route.
    /// Uses the last match because tolerance-based equality can produce multiple candidates.
    /// </summary>
    /// <param name="waypoints">Ordered route waypoints.</param>
    /// <param name="nextStop">Configured next-stop position.</param>
    /// <returns>Index of the best matching waypoint, or -1 if not found.</returns>
    private static int FindNextStopIndex(List<Position> waypoints, Position nextStop) =>
        waypoints.FindLastIndex(w => w == nextStop);

    /// <summary>
    /// Computes cumulative polyline length from index 0 up to <paramref name="endIndexInclusive"/>.
    /// </summary>
    /// <param name="waypoints">Ordered route waypoints.</param>
    /// <param name="endIndexInclusive">Last waypoint index included in the length computation.</param>
    /// <returns>Total distance in kilometers.</returns>
    private static double PathLength(IReadOnlyList<Position> waypoints, int endIndexInclusive)
    {
        if (waypoints.Count < 2 || endIndexInclusive <= 0)
            return 0;

        var clampedEnd = Math.Min(endIndexInclusive, waypoints.Count - 1);
        var total = 0.0;
        for (var i = 0; i < clampedEnd; i++)
            total += GeoMath.EquirectangularDistance(waypoints[i], waypoints[i + 1]);

        return total;
    }

    private void CheckIfTimeIsBeforeDeparture(Time time)
    {
        if (time < Current.Departure)
            throw new ArgumentException($"Current time: {time} is before the current journey has started: {Current.Departure}.");
    }

    private void CheckIfTimeIsAfterCompletion(Time time)
    {
        var completedTime = Current.Departure + Current.Duration;
        if (time > completedTime && !time.IsApproximately(completedTime))
            throw new ArgumentException($"Current time: {time} is after the journey has completed: {completedTime}. Overshoot: {time - completedTime}s.");
    }

    private void CheckIfTimeIsAfterEtaToNextStop(Time time)
    {
        if (time > Current.EtaToNextStop && !time.IsApproximately(Current.EtaToNextStop))
            throw new ArgumentException($"Current time: {time} is after ETA to next stop: {Current.EtaToNextStop}. Overshoot: {time - Current.EtaToNextStop}s.");
    }
}
