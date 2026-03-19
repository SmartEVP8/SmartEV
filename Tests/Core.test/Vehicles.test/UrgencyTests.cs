namespace Core.Tests.Vehicles;

using Core.Vehicles;
using Xunit;

public class UrgencyTests
{
    [Fact]
    public void CalculateChargeUrgency_ReturnsZero_WhenStateOfChargeIsAboveUpperChargeLimit()
    {
        double urgency = Urgency.CalculateChargeUrgency(90f, 20f);

        Assert.Equal(0.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ReturnsZero_WhenStateOfChargeIsExactlyAtUpperChargeLimit()
    {
        double urgency = Urgency.CalculateChargeUrgency(80f, 20f);

        Assert.Equal(0.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ReturnsOne_WhenStateOfChargeIsBelowMinimumAcceptableCharge()
    {
        double urgency = Urgency.CalculateChargeUrgency(10f, 20f);

        Assert.Equal(1.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ReturnsOne_WhenStateOfChargeIsExactlyAtMinimumAcceptableCharge()
    {
        double urgency = Urgency.CalculateChargeUrgency(20f, 20f);

        Assert.Equal(1.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ReturnsValueBetweenZeroAndOne_WhenStateOfChargeIsBetweenThresholds()
    {
        double urgency = Urgency.CalculateChargeUrgency(50f, 20f);

        Assert.InRange(urgency, 0.0, 1.0);
        Assert.NotEqual(0.0, urgency);
        Assert.NotEqual(1.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ReturnsHigherUrgency_ForLowerStateOfCharge()
    {
        double higherSocUrgency = Urgency.CalculateChargeUrgency(70f, 20f);
        double lowerSocUrgency = Urgency.CalculateChargeUrgency(40f, 20f);

        Assert.True(lowerSocUrgency > higherSocUrgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ClampsNegativeStateOfCharge_ToZero()
    {
        double urgency = Urgency.CalculateChargeUrgency(-10f, 20f);

        Assert.Equal(1.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ClampsStateOfChargeAboveOneHundred_ToOneHundred()
    {
        double urgency = Urgency.CalculateChargeUrgency(150f, 20f);

        Assert.Equal(0.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ClampsNegativeMinimumAcceptableCharge_ToZero()
    {
        double urgency = Urgency.CalculateChargeUrgency(50f, -10f);

        Assert.InRange(urgency, 0.0, 1.0);
        Assert.True(urgency > 0.0);
    }

    [Fact]
    public void CalculateChargeUrgency_ClampsMinimumAcceptableChargeAboveOneHundred_ToOneHundred()
    {
        double urgency = Urgency.CalculateChargeUrgency(50f, 150f);

        Assert.Equal(1.0, urgency);
    }

    [Fact]
    public void CalculateChargeUrgency_ReturnsValuesWithinRange_ForSeveralInputs()
    {
        float[] socValues = [0f, 10f, 20f, 40f, 60f, 79f, 80f, 100f];
        const float minAcceptableCharge = 20f;

        foreach (float soc in socValues)
        {
            double urgency = Urgency.CalculateChargeUrgency(soc, minAcceptableCharge);

            Assert.InRange(urgency, 0.0, 1.0);
        }
    }
}