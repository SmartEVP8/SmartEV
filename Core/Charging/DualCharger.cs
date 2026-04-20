namespace Core.Charging;

using Core.Charging.ChargingModel;
using Core.Shared;
using Core.Helper;

/// <summary>
/// Charger that supports one or two vehicles simultaneously, redistributing
/// surplus power from one side to the other.
/// </summary>
/// <param name="id">The id of the charger.</param>
/// <param name="maxPowerKW">The maximum power output in kilowatts.</param>
/// <param name="connectors">The connector set available at this charger.</param>
public sealed class DualCharger(int id, int maxPowerKW, Connectors connectors)
    : ChargerBase(id, maxPowerKW)
{
    private Connector _left = connectors.AttachedConnectors.Left;
    private Connector _right = connectors.AttachedConnectors.Right;

    /// <summary>
    /// Gets or sets the active charging session at side A, or null if free. Always used for single chargers.
    /// </summary>
    public ActiveSession? SessionA { get; set; }

    /// <summary>
    /// Gets or sets the active charging session at side B, or null if free. Always null for single chargers.
    /// </summary>
    public ActiveSession? SessionB { get; set; }

    /// <summary>
    /// Distributes power between two sides. Surplus from one side due to charging
    /// curve taper or car rate limit is offered to the other, subject to each
    /// connector's physical cap.
    /// </summary>
    /// <param name="maxKW">The total power available at the charger in kilowatts.</param>
    /// <param name="socA">The state of charge of the vehicle on the left side (0.0 to 1.0).</param>
    /// <param name="socB">The state of charge of the vehicle on the right side (0.0 to 1.0).</param>
    /// <param name="maxChargeRateKWA">The maximum charge rate of the vehicle on the left side in kilowatts.</param>
    /// <param name="maxChargeRateKWB">The maximum charge rate of the vehicle on the right side in kilowatts.</param>
    /// <returns>A tuple containing the power allocated to the left and right side in kilowatts.</returns>
    public (double PowerA, double PowerB) GetPowerDistribution(
        double maxKW,
        double socA,
        double socB,
        double maxChargeRateKWA,
        double maxChargeRateKWB)
    {
        if (maxKW <= 0)
            throw Log.Error(0, 0, new ArgumentOutOfRangeException(nameof(maxKW), $"maxKW must be positive. Received {maxKW}."), ("ChargerId", Id), ("maxKW", maxKW));
        var nominal = maxKW / 2.0;
        var fractionA = ChargingCurve.PowerFraction(socA);
        var fractionB = ChargingCurve.PowerFraction(socB);
        var physicalCapA = Math.Min(_left.PowerKW, maxChargeRateKWA);
        var physicalCapB = Math.Min(_right.PowerKW, maxChargeRateKWB);
        var ceilA = Math.Min(nominal, physicalCapA) * fractionA;
        var ceilB = Math.Min(nominal, physicalCapB) * fractionB;
        var surplusA = nominal - ceilA;
        var surplusB = nominal - ceilB;
        return (
            Math.Min(ceilA + Math.Max(0, surplusB), physicalCapA),
            Math.Min(ceilB + Math.Max(0, surplusA), physicalCapB));
    }

    /// <summary>
    /// Attempts to connect a vehicle to the first free side.
    /// </summary>
    /// <returns>The side the vehicle was connected to, or null if both sides are occupied.</returns>
    public ChargingSide? TryConnect()
    {
        if (TryActivate(ref _left)) return ChargingSide.Left;
        if (TryActivate(ref _right)) return ChargingSide.Right;
        return null;
    }

    /// <summary>Disconnects the vehicle on the given side.</summary>
    /// <param name="side">The side from which to disconnect the vehicle.</param>
    public void Disconnect(ChargingSide side)
    {
        if (side == ChargingSide.Left) _left.Deactivate();
        else _right.Deactivate();
    }

    private static bool TryActivate(ref Connector connector)
    {
        if (!connector.IsFree) return false;
        connector.Activate();
        return true;
    }

    /// <inheritdoc/>
    public override bool IsFree => SessionA is null || SessionB is null;

    /// <inheritdoc/>
    public override void AccumulateEnergy(Time now)
    {
        if (now <= Window.LastEnergyUpdateTime) return;

        if (SessionA is not null)
            Window = Window with { DeliveredKWh = Window.DeliveredKWh + SessionA.GetDeliveredKWh(Window.LastEnergyUpdateTime, now) };

        if (SessionB is not null)
            Window = Window with { DeliveredKWh = Window.DeliveredKWh + SessionB.GetDeliveredKWh(Window.LastEnergyUpdateTime, now) };

        Window = Window with { LastEnergyUpdateTime = now };
    }

    /// <inheritdoc/>
    public override void UpdateWindowStats()
    {
        var isActive = Queue.Count > 0
            || SessionA is not null
            || SessionB is not null
            || Window.DeliveredKWh > 0;

        if (!isActive)
            return;

        Window = Window with
        {
            HadActivity = true,
            QueueSize = Queue.Count,
        };
    }
}
