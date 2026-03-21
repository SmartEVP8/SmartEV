namespace Engine.test.Metrics;

using System;
using System.IO;
using System.Collections.Generic;
using Core.Charging;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;

internal static class SnapshotMetricsHelper
{
    internal static EnergyPrices MakeEnergyPrices()
    {
        var lines = new List<string> { "Day,Hour,Price" };
        foreach (var day in Enum.GetValues<DayOfWeek>())
            for (var h = 0; h < 24; h++) lines.Add($"{day},{h},3.00");

        var path = Path.GetTempFileName();
        File.WriteAllLines(path, lines);
        return new EnergyPrices(new FileInfo(path));
    }

    internal static Station MakeStation(List<ChargerBase>? chargers = null)
    {
        return new Station(
            id: 1,
            name: "Test Station",
            address: "Test Address",
            position: new Position(0, 0),
            chargers: chargers,
            random: new Random(42),
            energyPrices: MakeEnergyPrices()
        );
    }

    internal static SingleCharger MakeSingleCharger(int id, int maxPowerKW = 150)
    {
        var connectors = new Connectors([new Connector(Socket.CCS2)]);
        var point = new SingleChargingPoint(connectors);
        return new SingleCharger(id, maxPowerKW, point);
    }

    internal static DualCharger MakeDualCharger(int id, int maxPowerKW = 300)
    {
        var connectors = new Connectors([new Connector(Socket.CCS2)]);
        var point = new DualChargingPoint(connectors);
        return new DualCharger(id, maxPowerKW, point);
    }
}