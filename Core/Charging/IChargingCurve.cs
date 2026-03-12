namespace Core.Charging;

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
    double PowerFraction(double soc);
}

/// <summary>
/// Default curve with three regions:
///   soc &lt; 0.1  → ramp up   (0.5 → 1.0)
///   soc &lt; 0.8  → full power (1.0)
///   soc >= 0.8 → taper off  (1.0 → 0.2)
/// </summary>
public class DefaultChargingCurve : IChargingCurve
{
    public double PowerFraction(double soc)
    {
        if (soc < 0.1) return 0.5 + (5.0 * soc);
        if (soc < 0.8) return 1.0;
        return Math.Max(0.2, 1.0 - 3.0 * (soc - 0.8));
    }
}

/// <summary>
/// A curve that tapers more aggressively — starts tapering at 70% SoC
/// and drops to 0.1 at full charge. Models older or smaller battery packs.
/// </summary>
public class AggressiveTaperCurve : IChargingCurve
{
    public double PowerFraction(double soc)
    {
        if (soc < 0.1) return 0.4 + (4.0 * soc);
        if (soc < 0.7) return 1.0;
        return Math.Max(0.1, 1.0 - (3.0 * (soc - 0.7)));
    }
}
