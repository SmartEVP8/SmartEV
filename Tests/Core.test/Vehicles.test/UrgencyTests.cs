namespace Core.test.Vehicles.test;

using Core.Vehicles;

public class UrgencyTests
{
    [Fact]
    public void CalculateChargeUrgency_ReturnsZero_WhenStateOfChargeIsAtUpperBound()
    {
        var minCharge = 20f;

        var stateOfCharge = 80f;

        var urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minCharge);

        Assert.Equal(0.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ReturnsOne_WhenStateOfChargeIsAtMinimumAcceptableCharge()
    {
        var minCharge = 20f;

        var stateOfCharge = 20f;

        var urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minCharge);

        Assert.Equal(1.0, urgency);
    }
}