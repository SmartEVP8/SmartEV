namespace Engine.Benchmark.Charging;

using BenchmarkDotNet.Attributes;
using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Charging;

/// <summary>
/// Benchmark suite for ChargingSession performance testing.
/// </summary>
[MemoryDiagnoser]
public class ChargingSessionBenchmark
{
    private const double _availablePower = 22.0;
    private const double _socTarget = 0.8;
    private ChargingSession _singleSession = null!;
    private ChargingSession _dualSession = null!;
    private ChargingModel _chargingModel = null!;
    private EV _car1 = null!;
    private EV _car2 = null!;

    /// <summary>
    /// Initializes the benchmark setup with charging sessions and electric vehicles.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _chargingModel = new ChargingModel();

        var singlePoint = new SingleChargingPoint();
        var dualPoint = new DualChargingPoint(
            leftSide: [new Connector(Socket.Type2)],
            rightSide: [new Connector(Socket.Type2)]);

        _singleSession = new ChargingSession(_chargingModel, singlePoint, _availablePower);
        _dualSession = new ChargingSession(_chargingModel, dualPoint, _availablePower);

        var battery1 = new Battery(capacity: 60, maxChargeRate: 11, currentCharge: 0.2f, socket: Socket.Type2);
        var battery2 = new Battery(capacity: 40, maxChargeRate: 7, currentCharge: 0.3f, socket: Socket.Type2);
        var preferences = new Preferences(priceSensitivity: 0.5f);

        _car1 = new EV(id: 1, battery: battery1, preferences: preferences);
        _car2 = new EV(id: 2, battery: battery2, preferences: preferences);
    }

    /// <summary>
    /// Benchmarks starting and stopping a single car at a single charging point.
    /// </summary>
    [Benchmark]
    public void SinglePointStartStop()
    {
        _singleSession.StartCharging(_car1, _socTarget);
        _singleSession.StopCharging(_car1);
    }

    /// <summary>
    /// Benchmarks starting and stopping two cars at a dual charging point.
    /// </summary>
    [Benchmark]
    public void DualPointTwoCarsStartStop()
    {
        _dualSession.StartCharging(_car1, _socTarget);
        _dualSession.StartCharging(_car2, _socTarget);
        _dualSession.StopCharging(_car1);
        _dualSession.StopCharging(_car2);
    }

    /// <summary>
    /// Benchmarks the scenario where the first car leaves and the second car gets full power.
    /// </summary>
    [Benchmark]
    public void DualPointFirstCarLeaves()
    {
        _dualSession.StartCharging(_car1, _socTarget);
        _dualSession.StartCharging(_car2, _socTarget);
        _dualSession.StopCharging(_car1);
        _dualSession.StopCharging(_car2);
    }
}