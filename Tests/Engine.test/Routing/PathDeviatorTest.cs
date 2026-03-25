using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Engine.Routing;

public class PathDeviatorTest
{
    [Fact]
    public void CalculateDetourDeviation_DetourLongerThanOriginal_ReturnsDifference()
    {
        var ev = CreateEvWithOriginalDuration(originalDuration: 500);
        var detourJourney = (duration: 700f, polyline: "encoded_polyline");

        var deviation = PathDeviator.CalculateDetourDeviation(ref ev, detourJourney);

        Assert.Equal(200f, deviation);
    }

    [Fact]
    public void CalculateDetourDeviation_DetourShorterThanOriginal_ReturnsZero()
    {
        var ev = CreateEvWithOriginalDuration(originalDuration: 500);
        var detourJourney = (duration: 400f, polyline: "encoded_polyline");

        var deviation = PathDeviator.CalculateDetourDeviation(ref ev, detourJourney);

        Assert.Equal(0f, deviation);
    }

    [Fact]
    public void CalculateDetourDeviation_DetourEqualToOriginal_ReturnsZero()
    {
        var ev = CreateEvWithOriginalDuration(originalDuration: 500);
        var detourJourney = (duration: 500f, polyline: "encoded_polyline");

        var deviation = PathDeviator.CalculateDetourDeviation(ref ev, detourJourney);

        Assert.Equal(0f, deviation);
    }

    private static EV CreateEvWithOriginalDuration(uint originalDuration)
    {
        var battery = new Battery(capacity: 100, maxChargeRate: 150, stateOfCharge: 80, socket: Socket.CCS2);
        var preferences = new Preferences(priceSensitivity: 0.5f, minAcceptableCharge: 0.2f);
        var journey = new Journey(
            departure: new Time(0),
            originalDuration: new Time(originalDuration),
            path: new Paths([new Position(0, 0), new Position(1, 1)]));

        return new EV(battery, preferences, journey, efficiency: 150);
    }
}