namespace Core.test.Vehicles.test;

using Core.test.Builders;
using Core.Vehicles;

public class UrgencyTests
{
    [Fact]
    public void CalculateChargeUrgency_ReturnsZero_WhenStateOfChargeIsAtUpperBound()
    {
        var ev = CoreTestData.EV(battery: new Battery(capacity: 100, maxChargeRate: 150, stateOfCharge: 0.8f), preferences: new Preferences(priceSensitivity: 0, minAcceptableCharge: 0.2f, maxPathDeviationKm: 0));

        var urgency = Urgency.CalculateChargeUrgency(ref ev, 30);

        Assert.Equal(0.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ReturnsOne_WhenStateOfChargeIsAtMinimumAcceptableCharge()
    {
        var ev = CoreTestData.EV(battery: new Battery(capacity: 100, maxChargeRate: 150, stateOfCharge: 0.2f), preferences: new Preferences(priceSensitivity: 0, minAcceptableCharge: 0.2f, maxPathDeviationKm: 0));

        var urgency = Urgency.CalculateChargeUrgency(ref ev, 30);

        Assert.Equal(1.0, urgency);
    }
}
