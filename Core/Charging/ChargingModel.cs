namespace Core.Charging;

/// <summary>
/// Represents a model for estimating charging times based on the state of charge of a battery,
/// the capacity of the battery, and the power of the charger, using a reference table to account
/// for the varying charging rates at different states of charge.
/// </summary>
public class ChargingModel
{
    private readonly double[] _socGrid;
    private readonly double[] _tRef;
    private readonly double _ds;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChargingModel"/> class.
    /// </summary>
    /// <param name="stepSize">The step size for state of charge increments.</param>
    public ChargingModel(double stepSize = 0.001)
    {
        _ds = stepSize;

        var n = (int)(1.0 / _ds) + 1;

        _socGrid = new double[n];
        _tRef = new double[n];

        BuildReferenceTable();
    }

    /// <summary>
    /// Calculates the time required to charge a battery from a starting state of charge to a target 
    /// state of charge, given the battery capacity and the power of the charger,
    /// using a reference table that accounts for the varying charging rates.
    /// </summary>
    /// <param name="socStart">The starting state of charge.</param>
    /// <param name="socEnd">The target state of charge.</param>
    /// <param name="batteryCapacityKWh">The capacity of the battery in kWh.</param>
    /// <param name="chargerPowerKW">The power of the charger in kW.</param>
    /// <returns> The time required to charge the battery in hours. </returns>
    public double GetChargingTimeHours(
        double socStart,
        double socEnd,
        double batteryCapacityKWh,
        double chargerPowerKW)
    {
        if (socEnd <= socStart)
            return 0.0;

        var i1 = (int)(socStart / _ds);
        var i2 = (int)(socEnd / _ds);

        i1 = Math.Clamp(i1, 0, _tRef.Length - 1);
        i2 = Math.Clamp(i2, 0, _tRef.Length - 1);

        var referenceTime = _tRef[i2] - _tRef[i1];
        return batteryCapacityKWh / chargerPowerKW * referenceTime;
    }

    private double PowerFraction(double soc)
    {
        if (soc < 0.2)
            return 0.5 + (5 * soc);

        if (soc < 0.8)
            return 1.0;

        var taper = 1.0 - (3 * (soc - 0.8));
        return Math.Max(0.2, taper);
    }

    private void BuildReferenceTable()
    {
        _tRef[0] = 0.0;
        _socGrid[0] = 0.0;

        for (var i = 0; i < _socGrid.Length - 1; i++)
        {
            var s = i * _ds;
            _socGrid[i] = s;

            var powerFrac = PowerFraction(s);

            var dt = _ds / powerFrac;

            _tRef[i + 1] = _tRef[i] + dt;
        }

        _socGrid[^1] = 1.0;
    }
}
