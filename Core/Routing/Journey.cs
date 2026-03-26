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
    public Time Departure { get; } = departure;

    /// <summary>
    /// Gets the original duration of the journey, i.e. the duration of A -> B without any detours.
    /// </summary>
    public Time OriginalDuration { get; } = originalDuration;

    /// <summary>
    /// Gets the duration of an EVs journey, after it has been altered, i.e the duration of Start -> Station -> Detour.
    /// </summary>
    public Time JourneyDuration { get; private set; } = originalDuration;


    /// <summary>
    /// Gets the path as it is currently. 
    /// This can be updated as the journey progresses, e.g. if the EV is rerouted to a different station.
    /// Represented by waypoints that are mutated as the journey progresses.
    /// </summary>
    public Paths Path { get; private set; } = path;

    private float _runningSumDeviation;

    /// <summary>
    /// Calucates the EV's current position. Assumes the speed is always the same.
    /// </summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The position of the car.</returns>
    /// <exception cref="ArgumentException">Thrown when the current time is before the journey starts or after it has completed.</exception>
    public Position CurrentPosition(Time currentTime)
    {
        Time completedTime = Departure + JourneyDuration;
        if (currentTime > completedTime)
            throw new ArgumentException("Current time is after the journey has completed.");
        if (currentTime < Departure)
            throw new ArgumentException("Current time is before the journey has started.");

        var percentageCompleted = (currentTime - Departure) / (double)JourneyDuration;

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

    /// <summary>Calculates the times elapsed since the journey started.</summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The elapsed time.</returns>
    public Time TimeElapsed(Time currentTime) => currentTime - Departure;

    /// <summary>
    /// Gets the running sum of deviations for this journey. 
    /// Can be updated as the journey progresses using the UpdateRunningSumDeviation method.
    /// </summary>
    public float RunningSumDeviation => _runningSumDeviation;

    /// <summary> Updates the running sum deviation for this journey.</summary>
    /// <param name="deviation">The new deviation to set.</param>
    public void UpdateRunningSumDeviation(float deviation) => _runningSumDeviation = deviation;

    /// <summary>
    /// Updates the path of the journey. This can be used to update the path as the journey progresses, e.g. if the EV is rerouted to a different station.
    /// </summary>
    /// <param name="newPath">The new path for the journey.</param>
    public void UpdatePath(Paths newPath) => Path = newPath;

    /// <summary>
    /// Updates the journey's full duration. This is used to calculate an EVs position after its journey has been changed.
    /// </summary>
    /// <param name="newDuration"> The new duration of the EVs journey.</param>
    public void UpdateJourneyDuration(Time newDuration) => JourneyDuration = newDuration;
}
