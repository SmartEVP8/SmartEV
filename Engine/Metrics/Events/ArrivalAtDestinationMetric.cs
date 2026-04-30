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
    required public uint ExpectedArrivalTime { get; init; }

    /// <summary>
    /// Gets the actual arrival time at the destination for a journey.
    /// </summary>
    required public uint ActualArrivalTime { get; init; }

    /// <summary>
    /// Gets the path deviation for a journey.
    /// </summary>
    required public int PathDeviation { get; init; }

    /// <summary>
    /// Gets a value indicating whether an EV drives directly to its destination or goes charging.
    /// </summary>
    required public bool DriveDirectlyToDestination { get; init; }

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
    public static ArrivalAtDestinationMetric Collect(EV ev, Time simNow)
    {
        var baselineEnergyDemand = ev.EnergyForDistanceKWh(ev.Journey.Original.DistanceKm);
        var deadline = DeadlineCalculator.Calculate(
            ev.Journey,
            ev.SpawnStateOfCharge,
            ev.Preferences.MinAcceptableCharge,
            (float)ev.Preferences.MaxPathDeviation,
            ev.Battery.MaxCapacityKWh,
            baselineEnergyDemand);

        return new ArrivalAtDestinationMetric
        {
            ExpectedArrivalTime = ev.Journey.Original.Eta,
            ActualArrivalTime = simNow,
            PathDeviation = ev.Journey.Current.PathDeviation,
            MissedDeadline = simNow > deadline,
            DriveDirectlyToDestination = ev.DriveDirectlyToDestination,
        };
    }
}
