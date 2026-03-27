namespace Engine.Metrics.Events;

using Core.Shared;
using Core.Vehicles;

/// <summary>
/// Represents a metric related to deadlines, which can be used to track and analyze the timing of events and actions within the Engine.
/// </summary>
public readonly struct DeadlineMetric
{
    /// <summary>
    /// Gets the expected deadline for a journey.
    /// </summary>
    required public Time ExpectedDeadline { get; init; }

    /// <summary>
    /// Gets the actual arrival time at the destination for a journey.
    /// </summary>
    required public Time ActualArrivalTime { get; init; }

    /// <summary>
    /// Gets the path deviation for a journey.
    /// </summary>
    required public double PathDeviation { get; init; }

    /// <summary>
    /// Gets the difference between actual and expected deadline.
    /// </summary>
    public Time DeltaDeadline => ActualArrivalTime - ExpectedDeadline;

    /// <summary>
    /// Gets a value indicating whether the expected deadline was missed.
    /// </summary>
    public bool MissedDeadline => DeltaDeadline > 0;

    /// <summary>
    /// Collects a DeadlineMetric for a given EV.
    /// </summary>
    /// <param name="ev">The EV for which to collect metrics.</param>
    /// <param name="simNow">The actual time the EV arrived at the destination.</param>
    /// <returns>The collected deadline metric.</returns>
    public static DeadlineMetric Collect(ref EV ev, Time simNow)
    {
        var expectedDeadline = ev.Journey.JourneyStart + ev.Journey.OriginalDuration;

        return new DeadlineMetric
        {
            ExpectedDeadline = expectedDeadline,
            ActualArrivalTime = simNow,
            PathDeviation = ev.Journey.PathDeviation,
        };
    }
}
