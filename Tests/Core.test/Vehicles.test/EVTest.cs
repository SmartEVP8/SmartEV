namespace Core.test.Vehicles;

using Core.Vehicles;
using Core.Routing;
using Core.Shared;
using Engine.test.Builders;

public class EVTest
{
    [Fact]
    public void ConsumeEnergyTest()
    {
        var battery = new Battery(50, 0, 1f, Socket.CCS2);
        var ev = TestData.EV(battery: battery, efficiency: 150, originalDuration: 10800, distanceMeters: 300000);
        ev.ConsumeEnergy(0, 6000);
        Assert.Equal(0.5, ev.Battery.StateOfCharge, precision: 2);

        ev.ConsumeEnergy(6000, 7000);
        Assert.Equal(0.42, ev.Battery.StateOfCharge, precision: 2);
    }

    [Fact]
    public void CalcDesiredSoCTest()
    {
        var battery = new Battery(50, 0, 1f, Socket.CCS2);
        var ev = TestData.EV(battery: battery, efficiency: 150, originalDuration: 10800, distanceMeters: 300000);

        var desiredSoC = ev.CalcDesiredSoC(3000);
        Assert.Equal(0.75f, desiredSoC, precision: 2);

        desiredSoC = ev.CalcDesiredSoC(4000);
        Assert.Equal(0.67f, desiredSoC, precision: 2);

        desiredSoC = ev.CalcDesiredSoC(7000);
        Assert.Equal(0.42f, desiredSoC, precision: 2);
    }

    [Fact]
    public void CanReachTest()
    {
        var battery = new Battery(50, 0, 1f, Socket.CCS2);
        var ev = TestData.EV(battery: battery, efficiency: 150, originalDuration: 10800, distanceMeters: 300000);

        Assert.True(ev.CanReach(100));
        Assert.False(ev.CanReach(500));
    }
}
