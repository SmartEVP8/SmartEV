namespace Core.Charging.ChargingModel;

/* We can change to using the interface when we have defined which cars used which curves. For now I just hardcoded a specific curve.
/// <summary>
/// Any charging curve implements this. Swap in a different curve
/// without touching the integrator.
/// </summary>
public interface IChargingCurve
{
    /// <summary>
    /// Returns [0,1] — how much of its allocated power the car can
    /// actually absorb at this SoC. 1.0 = full power, 0.5 = half, etc.
    /// </summary>
    /// <param name="soc">State of charge, as a fraction between 0 and 1.</param>
    /// <returns>The fraction of allocated power the car can absorb at the given SoC.</returns>
    double PowerFraction(double soc);
}

/// <summary>
/// Default curve with three regions:
///   soc &lt; 0.1  → ramp up   (0.5 → 1.0)
///   soc &lt; 0.8  → full power (1.0)
///   soc >= 0.8 → taper off  (1.0 → 0.2).
/// </summary>
public class DefaultChargingCurve : IChargingCurve
{
    /// <summary>
    /// Calculates the fraction of allocated power the car can absorb at the given state of charge (SoC).
    /// </summary>
    /// <param name="soc">State of charge, as a fraction between 0 and 1.</param>
    /// <returns>The fraction of allocated power the car can absorb at the given SoC.</returns>
    public double PowerFraction(double soc)
    {
        if (soc < 0.1) return 0.5 + (5.0 * soc);
        if (soc < 0.8) return 1.0;
        return Math.Max(0.2, 1.0 - (3.0 * (soc - 0.8)));
    }
}
*/

/// <summary>
/// Models the charge acceptance curve of an EV battery.
/// Starts tapering at 70% SoC and drops to 0.1 at full charge.
/// </summary>
public static class ChargingCurve
{
    /// <summary>
    /// Returns [0,1] — how much of its allocated power the car can
    /// actually absorb at this SoC. 1.0 = full power, 0.5 = half, etc.
    /// </summary>
    /// <param name="soc">State of charge, as a fraction between 0 and 1.</param>
    /// <returns>The fraction of allocated power the car can absorb at the given SoC.</returns>
    public static double PowerFraction(double soc)
    {
        if (soc < 0.1) return 0.4 + (4.0 * soc);
        if (soc < 0.7) return 1.0;
        return Math.Max(0.1, 1.0 - (3.0 * (soc - 0.7)));
    }
}