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
    public readonly Time Departure = departure;
    public readonly Time OriginalDuration = originalDuration;
    public Paths Path = path;
    public float RunningSumDeviation = 0;

    /// <summary>
    /// Calucates the EV's current position. Assumes the speed is always the same.
    /// </summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The position of the car.</returns>
    /// <exception cref="ArgumentException">Thrown when the current time is before the journey starts or after it has completed.</exception>
    public Position CurrentPosition(Time currentTime)
    {
        Time completedTime = Departure + OriginalDuration;
        if (currentTime > completedTime)
            throw new ArgumentException("Current time is after the journey has completed.");
        if (currentTime < Departure)
            throw new ArgumentException("Current time is before the journey has started.");

        var percentageCompleted = (double)(currentTime - Departure) / (double)OriginalDuration;

        // TODO: might need a different distance but i think its fine.
        var segments = path
            .Waypoints.Zip(path.Waypoints.Skip(1))
            .Select(p =>
                (
                    p.First,
                    p.Second,
                    Length: Math.Sqrt(
                        Math.Pow(p.Second.Latitude - p.First.Latitude, 2)
                            + Math.Pow(p.Second.Longitude - p.First.Longitude, 2)
                    )
                )
            )
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

        return new Position(path.Waypoints[^1].Latitude, path.Waypoints[^1].Longitude);
    }

    public Time TimeElapsed(Time currentTime) => currentTime - Departure;
}
