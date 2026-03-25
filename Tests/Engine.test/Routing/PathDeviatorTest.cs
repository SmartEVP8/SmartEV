namespace Engine.test.Routing;

using Core.Shared;
using Core.Vehicles;
using Engine.Routing;
using Engine.test.Builders;

public class PathDeviatorTest
{
    [Fact]
    public void CalculateDetourDeviation_DetourLongerThanOriginal_ReturnsDifference()
    {
        var journey = TestData.Journey(
            waypoints: [new Position(0, 0), new Position(1, 1)],
            originalDuration: new Time(500));
        var ev = new EV(TestData.Battery(), TestData.Preferences(), journey, efficiency: 150);
        var detourJourney = (duration: 700f, polyline: "encoded_polyline");

        var deviation = PathDeviator.CalculateDetourDeviation(ref ev, detourJourney);

        Assert.Equal(200f, deviation);
    }

    [Fact]
    public void CalculateDetourDeviation_DetourShorterThanOriginal_ReturnsZero()
    {
        var journey = TestData.Journey(
            waypoints: [new Position(0, 0), new Position(1, 1)],
            originalDuration: new Time(500));
        var ev = new EV(TestData.Battery(), TestData.Preferences(), journey, efficiency: 150);
        var detourJourney = (duration: 400f, polyline: "encoded_polyline");

        var deviation = PathDeviator.CalculateDetourDeviation(ref ev, detourJourney);

        Assert.Equal(0f, deviation);
    }

    [Fact]
    public void CalculateDetourDeviation_DetourEqualToOriginal_ReturnsZero()
    {
        var journey = TestData.Journey(
            waypoints: [new Position(0, 0), new Position(1, 1)],
            originalDuration: new Time(500));
        var ev = new EV(TestData.Battery(), TestData.Preferences(), journey, efficiency: 150);
        var detourJourney = (duration: 500f, polyline: "encoded_polyline");

        var deviation = PathDeviator.CalculateDetourDeviation(ref ev, detourJourney);

        Assert.Equal(0f, deviation);
    }
}