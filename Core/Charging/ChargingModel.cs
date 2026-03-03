namespace Core.Charging;

public class ChargingModel
{
    private readonly double[] _socGrid;
    private readonly double[] _tRef;
    private readonly double _ds;


    public ChargingModel(double stepSize = 0.001)
    {
        _ds = stepSize;

        var n = (int)(1.0 / _ds) + 1;

        _socGrid = new double[n];
        _tRef = new double[n];

        BuildReferenceTable();
    }

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
        if (soc < 0.1)
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
