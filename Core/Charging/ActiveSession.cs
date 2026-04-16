namespace Core.Charging;

using Core.Charging.ChargingModel;
using Core.Shared;
public record ActiveSession(
    int EVId,
    ConnectedEV EV,
    Time StartTime,
    ChargingSide? Side,
    uint? CancellationToken,
    IntegrationResult? Plan)
{
    /// <summary>
    /// Gets the current SoC of the EV in the session based on the delivered energy in the plan up to the given simulation time.
    /// </summary>
    /// <param name="simNow">The simulation time.</param>
    /// <returns>The current SoC.</returns>
    public double GetCurrentSoC(Time simNow)
    {
        if (Plan is null) return EV.CurrentSoC;
        var delivered = GetEnergyFromCurve(GetCurve(), Plan.StepSeconds, StartTime, StartTime, simNow);
        return EV.CurrentSoC + (delivered / EV.CapacityKWh);
    }

    /// <summary>
    /// Gets the amount of energy delivered in the session between two simulation times.
    /// </summary>
    /// <param name="from">The start time.</param>
    /// <param name="to">The end time.</param>
    /// <returns>The delivered energy in kWh.</returns>
    public double GetDeliveredKWh(Time from, Time to)
    {
        if (Plan is null) return 0.0;
        return GetEnergyFromCurve(GetCurve(), Plan.StepSeconds, StartTime, from, to);
    }

    private List<double> GetCurve() =>
        (Side is ChargingSide.Right ? Plan?.CarB?.CumulativeEnergy : null)
        ?? Plan!.CarA.CumulativeEnergy;

    /// <summary>
    /// Helper method to calculate energy delivered from a cumulative energy curve between two simulation times.
    /// </summary>
    /// <param name="curve">The cumulative energy curve.</param>
    /// <param name="stepSeconds">The step seconds.</param>
    /// <param name="sessionStart">The session start time.</param>
    /// <param name="lastUpdate">The last update time.</param>
    /// <param name="simNow">The simulation time.</param>
    /// <returns>The delivered energy in kWh.</returns>
    private static double GetEnergyFromCurve(List<double> curve, uint stepSeconds, Time sessionStart, Time lastUpdate, Time simNow)
    {
        if (curve.Count == 0) return 0.0;
        var startOffset = Math.Max(0, lastUpdate - sessionStart);
        var endOffset = Math.Max(0, simNow - sessionStart);
        var startIndex = Math.Min(curve.Count - 1, (int)(startOffset / stepSeconds));
        var endIndex = Math.Min(curve.Count - 1, (int)(endOffset / stepSeconds));
        return curve[endIndex] - curve[startIndex];
    }
}
