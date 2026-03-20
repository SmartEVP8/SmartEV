namespace Core.Tests.Vehicles;

using Core.Vehicles;

public class UrgencyTests
{

    [Fact]
    public void CalculateChargeUrgency_ReturnsZero_WhenStateOfChargeIsAtUpperBound()
    {
        float minCharge = 20f;

        float stateOfCharge = 80f;

        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minCharge);

        Assert.Equal(0.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ReturnsOne_WhenStateOfChargeIsAtMinimumAcceptableCharge()
    {
        float minCharge = 20f;

        float stateOfCharge = 20f;

        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minCharge);

        Assert.Equal(1.0, urgency);
    }
}