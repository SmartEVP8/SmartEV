namespace Engine.test.Events;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;
using Engine.Events;
using Engine.Metrics;
using Engine.Services;
using Engine.Charging;
using Core.Vehicles;

public class ArriveAtStationEventHandlerTests
{
    [Fact]
    public void EV_Arrives_NoCompatibleCharger_NotQueued()
    {
        var (handler, metrics, startChargingCalls) = Build(socket: Socket.CCS2);
        var car = MakeCar(1, socket: Socket.CHADEMO);

        handler.Handle(new ArriveAtStation(1, 1, 0), car);

        Assert.Equal(0, metrics.TotalQueueSize);
        Assert.Empty(startChargingCalls);
        Assert.Empty(metrics.ArrivalTimes); // no arrival recorded for incompatible car
    }

    [Fact]
    public void EV_Arrives_ChargerFree_QueuedAndChargingStarts()
    {
        var (handler, metrics, startChargingCalls) = Build();
        var car = MakeCar(1);

        handler.Handle(new ArriveAtStation(1, 1, 0), car);

        Assert.Equal(0, metrics.TotalQueueSize);
        Assert.Single(startChargingCalls);
        Assert.Equal(0, startChargingCalls[0].Item2);
        Assert.Equal(0, metrics.ArrivalTimes[1]); // arrival time recorded
    }

    [Fact]
    public void EV_Arrives_ChargerFull_QueuedChargingDoesNotStart()
    {
        var (handler, metrics, startChargingCalls) = Build();

        handler.Handle(new ArriveAtStation(1, 1, 0), MakeCar(1));
        handler.Handle(new ArriveAtStation(2, 1, 50), MakeCar(2));
        handler.Handle(new ArriveAtStation(3, 1, 100), MakeCar(3));

        Assert.Equal(2, metrics.TotalQueueSize);
        Assert.Single(startChargingCalls);
        Assert.Equal(0, metrics.ArrivalTimes[1]);
        Assert.Equal(50, metrics.ArrivalTimes[2]);
        Assert.Equal(100, metrics.ArrivalTimes[3]);
    }

    [Fact]
    public void QueueSize_IncrementsCorrectly()
    {
        var (handler, metrics, _) = Build();

        handler.Handle(new ArriveAtStation(1, 1, 0), MakeCar(1));
        Assert.Equal(0, metrics.TotalQueueSize);

        handler.Handle(new ArriveAtStation(2, 1, 0), MakeCar(2));
        Assert.Equal(1, metrics.TotalQueueSize);

        handler.Handle(new ArriveAtStation(3, 1, 0), MakeCar(3));
        Assert.Equal(2, metrics.TotalQueueSize);
    }

    private static ConnectedCar MakeCar(uint evId, Socket socket = Socket.CCS2)
    {
        var model = EVModels.Models.First(m => m.Model == "Volkswagen ID.3");
        return new ConnectedCar(
            CarId: (int)evId,
            CurrentSoC: 0.2,
            TargetSoC: 0.8,
            CapacityKWh: model.BatteryConfig.MaxCapacityKWh,
            MaxChargeRateKW: model.BatteryConfig.ChargeRateKW,
            Socket: socket);
    }

    private static (ArriveAtStationEventHandler handler, StationSnapshotMetric metrics, List<(ChargerState, int)> startChargingCalls)
        Build(Socket socket = Socket.CCS2, int maxPowerKW = 150)
    {
        var connector = new Connector(socket);
        var point = new SingleChargingPoint(new Connectors([connector]));
        var charger = new SingleCharger(1, maxPowerKW, point);
        var chargerState = new ChargerState(charger);
        var stationChargers = new Dictionary<ushort, List<ChargerState>>
        {
            [1] = [chargerState],
        };

        var metrics = new StationSnapshotMetric();
        var startChargingCalls = new List<(ChargerState, int)>();
        void StartCharging(ChargerState cs, int t)
        {
            startChargingCalls.Add((cs, t));

            // simulate occupying the charger so IsFree returns false
            cs.SessionA = new ChargingSession(999, MakeCar(999), 0, null);
        }

        var handler = new ArriveAtStationEventHandler(stationChargers, StartCharging, metrics);
        return (handler, metrics, startChargingCalls);
    }


}