namespace Engine.test.Metrics;

using System;
using Engine.Metrics;
using Xunit;
using System.IO;
using System.Collections.Generic;
using Core.Charging;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;

public class SnapshotMetricTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 2)]
    public void ActiveChargers_IsCorrect(int enqueuedOnA, int enqueuedOnB, int expectedActive)
    {
        var chargerA = MakeSingleCharger(id: 1);
        var chargerB = MakeSingleCharger(id: 2);
        for (var i = 0; i < enqueuedOnA; i++) chargerA.Queue.Enqueue(i);
        for (var i = 0; i < enqueuedOnB; i++) chargerB.Queue.Enqueue(i);
        var station = MakeStation([chargerA, chargerB]);

        var metric = SnapshotMetric.Collect(station, 0, DayOfWeek.Monday, 0, _ => 0);

        Assert.Equal(expectedActive, metric.ActiveChargers);
    }

    private static EnergyPrices MakeEnergyPrices()
    {
        var lines = new List<string> { "Day,Hour,Price" };
        foreach (var day in Enum.GetValues<DayOfWeek>())
            for (var h = 0; h < 24; h++) lines.Add($"{day},{h},3.00");

        var path = Path.GetTempFileName();
        File.WriteAllLines(path, lines);
        return new EnergyPrices(new FileInfo(path));
    }

    private static Station MakeStation(List<ChargerBase> chargers)
    {
        return new Station(
            id: 1,
            name: "Test Station",
            address: "Test Address",
            position: new Position(0, 0),
            chargers: chargers,
            random: new Random(42),
            energyPrices: MakeEnergyPrices());
    }

    private static SingleCharger MakeSingleCharger(int id, int maxPowerKW = 150)
    {
        var connectors = new Connectors([new Connector(Socket.CCS2)]);
        var point = new SingleChargingPoint(connectors);
        return new SingleCharger(id, maxPowerKW, point);
    }
}
