namespace Core.Routing;

using Core.Shared;
using Core.GeoMath;

/// <summary>
/// Represents a journey for an electric vehicle.
/// </summary>
/// <param name="departure">The time the journey started.</param>
/// <param name="duration">The original duration of the journey.</param>
/// <param name="distanceMeters">The distance of the journey.</param>
/// <param name="path">The path of the journey.</param>
public class Journey(Time departure, Time duration, float distanceMeters, Paths path)
{
    /// <summary>
    /// Gets the time the journey started.
    /// </summary>
    public Time JourneyStart => departure;

    /// <summary>
    /// Gets the time the journey was last updated.
    /// </summary>
    public Time LastUpdatedDeparture { get; private set; } = departure;

    /// <summary>
    /// Gets the original duration of the journey, i.e. the duration of A -> B without any detours.
    /// </summary>
    public Time OriginalDuration => duration;

    /// <summary>
    /// Gets the duration of an EVs journey, after it has been altered, i.e the duration of Start -> Station -> Detour.
    /// </summary>
    public Time LastUpdatedDuration { get; private set; } = duration;

    /// <summary>
    /// Gets the current Path.
    /// </summary>
    public Paths Path { get; private set; } = path;

    /// <summary>
    /// Gets the next stop of the journey. Initially set to the original destination, but can be updated if the EV is rerouted through a station.
    /// </summary>
    public Position NextStop { get; private set; } = path.Waypoints[^1];

    /// <summary>
    /// Gets the additional time has been added by rerouting/deviating from the original path.
    /// </summary>
    public Time PathDeviation { get; private set; } = 0;

    /// <summary>Calculates the time elapsed since the journey started.</summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The elapsed time.</returns>
    public Time TimeElapsed(Time currentTime) => currentTime - JourneyStart;

    /// <summary>
    /// Gets the original distance of the journey, i.e. the distance of A -> B without any detours.
    /// </summary>
    public float OriginalDistancekm { get; } = distanceMeters / 1000;

    /// <summary>
    /// Gets the distance of an EVs journey, after it has been altered, i.e the distance of Start -> Station -> Detour.
    /// </summary>
    public float LastUpdatedDistancekm { get; private set; } = distanceMeters / 1000;

    public Paths GetPathFromCurrentPosition(Time currentTime)
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
                var postions = new Position(longitude: longitude, latitude: latitude);
                return new Paths([postions, .. Path.Waypoints.SkipWhile(w => w != second)]);
            }

            distanceCovered += length;
        }

        var last = Path.Waypoints[^1];
        return new Paths([new Position(longitude: last.Longitude, latitude: last.Latitude)]);
    }

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
    /// <param name="nextStop">The next stop/position of <paramref name="newRoute"/>.</param>
    /// <param name="departure">The the journey takes affect/is updated.</param>
    /// <param name="duration">The duration of the new joruney.</param>
    /// <param name="newDistancekm">The distance of the new journey.</param>
    public void UpdateRoute(Paths newRoute, Position nextStop, Time departure, Time duration, float newDistancekm)
    {
        Time oldEta = LastUpdatedDeparture + LastUpdatedDuration;
        Time newEta = departure + duration;

        Path = newRoute;
        NextStop = nextStop;
        LastUpdatedDeparture = departure;
        LastUpdatedDuration = duration;
        LastUpdatedDistancekm = newDistancekm;
        PathDeviation += newEta - oldEta;
    }

    /// <summary>
    /// Calculates the distance from the EV's current position to the next stop.
    /// </summary>
    /// <param name="currentTime">The current time.</param>
    /// <returns>Returns the distance to the next stop.</returns>
    public float DistanceToNextStop(Time currentTime)
    {
        var currentPos = CurrentPosition(currentTime);
        return (float)GeoMath.EquirectangularDistance(currentPos, NextStop) / 1000;
    }
}
