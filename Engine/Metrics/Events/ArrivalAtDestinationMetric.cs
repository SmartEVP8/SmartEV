namespace Engine.Metrics.Events;

using Core.Shared;
using Core.Vehicles;

/// <summary>
/// Represents a metric related to deadlines, which can be used to track and analyze the timing of events and actions within the Engine.
/// </summary>
public readonly struct ArrivalAtDestinationMetric
{
    /// <summary>
    /// Gets the expected arrival time at the destination for a journey.
    /// </summary>
    required public Time ExpectedArrivalTime { get; init; }

    /// <summary>
    /// Gets the actual arrival time at the destination for a journey.
    /// </summary>
    required public Time ActualArrivalTime { get; init; }

    /// <summary>
    /// Gets the path deviation for a journey.
    /// </summary>
    required public Time PathDeviation { get; init; }

    /// <summary>
    /// Gets the difference between actual and expected arrival time.
    /// </summary>
    public Time DeltaArrivalTime => ActualArrivalTime - ExpectedArrivalTime;

    /// <summary>
    /// Gets a value indicating whether the expected arrival time was missed.
    /// </summary>
    required public bool MissedDeadline { get; init; }

    /// <summary>
    /// Collects a ArrivalAtDestinationMetric for a given EV.
    /// </summary>
    /// <param name="ev">The EV for which to collect metrics.</param>
    /// <param name="simNow">The current simulation time, used to calculate actual arrival time.</param>
    /// <returns>The collected arrival metric.</returns>
    public static ArrivalAtDestinationMetric Collect(ref EV ev, Time simNow)
    {
        var expectedArrivalTime = ev.Journey.OriginalDuration;
        var actualArrivalTime = simNow - ev.Journey.JourneyStart;

        return new ArrivalAtDestinationMetric
        {
            ExpectedArrivalTime = expectedArrivalTime,
            ActualArrivalTime = actualArrivalTime,
            PathDeviation = ev.Journey.PathDeviation,
            MissedDeadline = actualArrivalTime > expectedArrivalTime,
        };
    }
}
