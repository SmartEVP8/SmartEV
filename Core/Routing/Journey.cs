namespace Core.Routing;

using Core.Shared;

/// <summary>
/// Represents a journey for an electric vehicle.
/// </summary>
/// <param name="departure">The time the journey started.</param>
/// <param name="originalDuration">The original duration of the journey.</param>
/// <param name="path">The path of the journey.</param>
public class Journey(Time departure, Time originalDuration, Paths path)
{
    /// <summary>
    /// Gets the time the journey started.
    /// </summary>
    public Time JourneyStart { get; } = departure;

    /// <summary>
    /// Gets the time the journey was last updated.
    /// </summary>
    public Time LastUpdatedDeparture { get; private set; } = departure;

    /// <summary>
    /// Gets the original duration of the journey, i.e. the duration of A -> B without any detours.
    /// </summary>
    public Time OriginalDuration { get; } = originalDuration;

    /// <summary>
    /// Gets the duration of an EVs journey, after it has been altered, i.e the duration of Start -> Station -> Detour.
    /// </summary>
    public Time LastUpdatedDuration { get; private set; } = originalDuration;

    /// <summary>
    /// Gets the current Path.
    /// </summary>
    public Paths Path { get; private set; } = path;

    /// <summary>
    /// Gets the additional time has been added by rerouting/deviating from the original path.
    /// </summary>
    public Time PathDeviation { get; private set; } = 0;

    /// <summary>Calculates the time elapsed since the journey started.</summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The elapsed time.</returns>
    public Time TimeElapsed(Time currentTime) => currentTime - JourneyStart;

    /// <summary>
    /// Calucates the EV's current position. Assumes the speed is always the same.
    /// </summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The position of the car.</returns>
    /// <exception cref="ArgumentException">Thrown when the current time is before the journey starts or after it has completed.</exception>
    public Position CurrentPosition(Time currentTime)
    {
        Time completedTime = LastUpdatedDeparture + LastUpdatedDuration;
        if (currentTime > completedTime)
            throw new ArgumentException($"Current time: {currentTime} is after the journey has completed: {completedTime}.");
        if (currentTime < LastUpdatedDeparture)
            throw new ArgumentException($"Current time: {currentTime} is before the journey has started: {JourneyStart}.");

        var percentageCompleted = (currentTime - LastUpdatedDeparture) / (double)LastUpdatedDuration;

        var segments = Path
            .Waypoints.Zip(Path.Waypoints.Skip(1))
            .Select(p =>
                (
                    p.First,
                    p.Second,
                    Length: Math.Sqrt(
                        Math.Pow(p.Second.Latitude - p.First.Latitude, 2)
                            + Math.Pow(p.Second.Longitude - p.First.Longitude, 2))
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
                return new Position(longitude: longitude, latitude: latitude);
            }

            distanceCovered += length;
        }

        var last = Path.Waypoints[^1];
        return new Position(longitude: last.Longitude, latitude: last.Latitude);
    }

    /// <summary>
    /// Updates the route of the journey and calculates the new path deviation based on the new estimated time of arrival (ETA) compared to the old ETA.
    /// </summary>
    /// <param name="newRoute">The new path/journey.</param>
    /// <param name="departure">The the journey takes affect/is updated.</param>
    /// <param name="duration">The duration of the new joruney.</param>
    public void UpdateRoute(Paths newRoute, Time departure, Time duration)
    {
        Path = newRoute;

        Time oldEta = LastUpdatedDeparture + LastUpdatedDuration;
        Time newEta = departure + duration;

        if (newEta > oldEta)
        {
            PathDeviation += newEta - oldEta;
        }
        else if (oldEta > newEta)
        {
            PathDeviation -= oldEta - newEta;
        }

        LastUpdatedDeparture = departure;
        LastUpdatedDuration = duration;
    }
}
