using Core.Charging;

/// <summary>
/// Tests for <see cref="ChargingModel"/>.
/// </summary>
public class ChargingModelTest
{
    private readonly ChargingModel _model = new();

    /// <summary>
    /// Verifies that <see cref="ChargingModel.GetChargingTimeHours"/> returns a positive time
    /// when charging from a lower SOC to a higher SOC.
    /// </summary>
    [Fact]
    public void GetChargingTimeHours_ValidRange_ReturnsPositiveTime()
    {
        var result = _model.GetChargingTimeHours(0.2, 0.8, 77.0, 150.0);

        Assert.True(result > 0);
    }

    /// <summary>
    /// Verifies that <see cref="ChargingModel.GetChargingTimeHours"/> returns zero
    /// when the target SOC is less than or equal to the starting SOC.
    /// </summary>
    /// <param name="socStart">The starting state of charge.</param>
    /// <param name="socEnd">The target state of charge.</param>
    [Theory]
    [InlineData(0.8, 0.2)]
    [InlineData(0.5, 0.5)]
    public void GetChargingTimeHours_EndNotGreaterThanStart_ReturnsZero(double socStart, double socEnd)
    {
        var result = _model.GetChargingTimeHours(socStart, socEnd, 77.0, 150.0);

        Assert.Equal(0.0, result);
    }

    /// <summary>
    /// Verifies that <see cref="ChargingModel.GetChargingTimeHours"/> returns a longer time
    /// in the taper zone (80–100%) than in the full power zone (20–80%) for the same SOC range.
    /// </summary>
    [Fact]
    public void GetChargingTimeHours_TaperZoneSlowerThanFullPowerZone()
    {
        var fullPowerTime = _model.GetChargingTimeHours(0.2, 0.3, 77.0, 150.0);
        var taperTime = _model.GetChargingTimeHours(0.8, 0.9, 77.0, 150.0);

        Assert.True(taperTime > fullPowerTime);
    }

    /// <summary>
    /// Verifies that <see cref="ChargingModel.GetChargingTimeHours"/> returns a longer time
    /// for a larger battery than a smaller one over the same SOC range.
    /// </summary>
    /// <param name="smallCapacity">The capacity of the smaller battery in kWh.</param>
    /// <param name="largeCapacity">The capacity of the larger battery in kWh.</param>
    [Theory]
    [InlineData(40.0, 77.0)]
    [InlineData(77.0, 100.0)]
    public void GetChargingTimeHours_LargerBatteryTakesLonger(double smallCapacity, double largeCapacity)
    {
        var smallTime = _model.GetChargingTimeHours(0.2, 0.8, smallCapacity, 150.0);
        var largeTime = _model.GetChargingTimeHours(0.2, 0.8, largeCapacity, 150.0);

        Assert.True(largeTime > smallTime);
    }

    /// <summary>
    /// Verifies that <see cref="ChargingModel.GetChargingTimeHours"/> returns a shorter time
    /// with a more powerful charger than a less powerful one.
    /// </summary>
    [Fact]
    public void GetChargingTimeHours_HigherPowerResultsInShorterTime()
    {
        var slowTime = _model.GetChargingTimeHours(0.2, 0.8, 77.0, 50.0);
        var fastTime = _model.GetChargingTimeHours(0.2, 0.8, 77.0, 150.0);

        Assert.True(fastTime < slowTime);
    }
}