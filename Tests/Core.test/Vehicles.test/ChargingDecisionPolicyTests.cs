namespace Core.Tests.Vehicles;

using Core.Vehicles;

public class ChargingDecisionPolicyTests
{
    [Fact]
    public void ShouldStopSearching_ReturnsTrue_WhenRemainingDistanceIsBelowStopSearchDistance()
    {
        var config = new ChargingDecisionConfig { StopSearchDistance = 10.0 };

        bool result = ChargingDecisionPolicy.ShouldStopSearching(5.0, config);

        Assert.True(result);
    }

    [Fact]
    public void ShouldStopSearching_ReturnsTrue_WhenRemainingDistanceIsEqualToStopSearchDistance()
    {
        var config = new ChargingDecisionConfig { StopSearchDistance = 10.0 };

        bool result = ChargingDecisionPolicy.ShouldStopSearching(10.0, config);

        Assert.True(result);
    }

    [Fact]
    public void ShouldStopSearching_ReturnsFalse_WhenRemainingDistanceIsAboveStopSearchDistance()
    {
        var config = new ChargingDecisionConfig { StopSearchDistance = 10.0 };

        bool result = ChargingDecisionPolicy.ShouldStopSearching(10.1, config);

        Assert.False(result);
    }

    [Fact]
    public void ShouldSearchForStation_ReturnsFalse_WhenStopSearchingConditionIsMet()
    {
        var config = new ChargingDecisionConfig { StopSearchDistance = 10.0 };

        bool result = ChargingDecisionPolicy.ShouldSearchForStation(10f, 20f, 5.0, config);

        Assert.False(result);
    }

    [Fact]
    public void ShouldReevaluateReservation_ReturnsFalse_WhenStopSearchingConditionIsMet()
    {
        var config = new ChargingDecisionConfig { StopSearchDistance = 10.0 };

        bool result = ChargingDecisionPolicy.ShouldReevaluateReservation(10f, 20f, 5.0, config);

        Assert.False(result);
    }

    [Fact]
    public void ShouldTriggerImmediateSearch_ReturnsFalse_WhenStopSearchingConditionIsMet()
    {
        var config = new ChargingDecisionConfig { StopSearchDistance = 10.0 };

        bool result = ChargingDecisionPolicy.ShouldTriggerImmediateSearch(10f, 20f, 5.0, config);

        Assert.False(result);
    }

    [Fact]
    public void ShouldSearchForStation_ReturnsTrue_WhenUrgencyEqualsMinimumUrgencyThreshold()
    {
        const float stateOfCharge = 50f;
        const float minAcceptableCharge = 20f;
        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minAcceptableCharge);

        var config = new ChargingDecisionConfig
        {
            MinimumUrgencyThreshold = urgency,
            StopSearchDistance = 10.0,
        };

        bool result = ChargingDecisionPolicy.ShouldSearchForStation(
            stateOfCharge,
            minAcceptableCharge,
            50.0,
            config);

        Assert.True(result);
    }

    [Fact]
    public void ShouldSearchForStation_ReturnsFalse_WhenUrgencyIsBelowMinimumUrgencyThreshold()
    {
        const float stateOfCharge = 50f;
        const float minAcceptableCharge = 20f;
        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minAcceptableCharge);

        var config = new ChargingDecisionConfig
        {
            MinimumUrgencyThreshold = urgency + 0.01,
            StopSearchDistance = 10.0,
        };

        bool result = ChargingDecisionPolicy.ShouldSearchForStation(
            stateOfCharge,
            minAcceptableCharge,
            50.0,
            config);

        Assert.False(result);
    }

    [Fact]
    public void ShouldReevaluateReservation_ReturnsTrue_WhenUrgencyEqualsReevaluateUrgencyThreshold()
    {
        const float stateOfCharge = 45f;
        const float minAcceptableCharge = 20f;
        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minAcceptableCharge);

        var config = new ChargingDecisionConfig
        {
            ReevaluateUrgency = urgency,
            StopSearchDistance = 10.0,
        };

        bool result = ChargingDecisionPolicy.ShouldReevaluateReservation(
            stateOfCharge,
            minAcceptableCharge,
            50.0,
            config);

        Assert.True(result);
    }

    [Fact]
    public void ShouldReevaluateReservation_ReturnsFalse_WhenUrgencyIsBelowReevaluateUrgencyThreshold()
    {
        const float stateOfCharge = 45f;
        const float minAcceptableCharge = 20f;
        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minAcceptableCharge);

        var config = new ChargingDecisionConfig
        {
            ReevaluateUrgency = urgency + 0.01,
            StopSearchDistance = 10.0,
        };

        bool result = ChargingDecisionPolicy.ShouldReevaluateReservation(
            stateOfCharge,
            minAcceptableCharge,
            50.0,
            config);

        Assert.False(result);
    }

    [Fact]
    public void ShouldTriggerImmediateSearch_ReturnsTrue_WhenUrgencyEqualsCriticalUrgencyThreshold()
    {
        const float stateOfCharge = 35f;
        const float minAcceptableCharge = 20f;
        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minAcceptableCharge);

        var config = new ChargingDecisionConfig
        {
            CriticalUrgency = urgency,
            StopSearchDistance = 10.0,
        };

        bool result = ChargingDecisionPolicy.ShouldTriggerImmediateSearch(
            stateOfCharge,
            minAcceptableCharge,
            50.0,
            config);

        Assert.True(result);
    }

    [Fact]
    public void ShouldTriggerImmediateSearch_ReturnsFalse_WhenUrgencyIsBelowCriticalUrgencyThreshold()
    {
        const float stateOfCharge = 35f;
        const float minAcceptableCharge = 20f;
        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minAcceptableCharge);

        var config = new ChargingDecisionConfig
        {
            CriticalUrgency = urgency + 0.01,
            StopSearchDistance = 10.0,
        };

        bool result = ChargingDecisionPolicy.ShouldTriggerImmediateSearch(
            stateOfCharge,
            minAcceptableCharge,
            50.0,
            config);

        Assert.False(result);
    }
}