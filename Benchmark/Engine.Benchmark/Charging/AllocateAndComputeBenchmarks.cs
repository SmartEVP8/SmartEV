namespace Engine.Benchmark.Charging;

using BenchmarkDotNet.Attributes;
using Core.Charging;
using Core.Shared;
using Core.Vehicles;

/// <summary>
/// Benchmark suite for AllocateAndCompute performance testing on charging points.
/// </summary>
[MemoryDiagnoser]
public class AllocateAndComputeBenchmark
{
    private const double _availablePower = 22.0;
    private const double _socTarget = 0.8;
    private SingleChargingPoint _singleChargingPoint;
    private DualChargingPoint _dualChargingPoint = null!;
    private ChargingModel _chargingModel = null!;
    private GetBattery _battery1 = null!;
    private GetBattery _battery2 = null!;

    /// <summary>
    /// Initializes the benchmark setup with charging points and battery snapshots.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _chargingModel = new ChargingModel();
        _singleChargingPoint = new SingleChargingPoint();
        _dualChargingPoint = new DualChargingPoint(
            leftSide: [new Connector(Socket.Type2)],
            rightSide: [new Connector(Socket.Type2)]);

        _battery1 = new GetBattery(MaxChargeRate: 11, CurrentCharge: 0.2f, Capacity: 60);
        _battery2 = new GetBattery(MaxChargeRate: 7, CurrentCharge: 0.3f, Capacity: 40);
    }

    /// <summary>
    /// Benchmarks allocating and computing charging time for a single charging point.
    /// </summary>
    [Benchmark]
    public void SinglePoint() =>
        _ = _singleChargingPoint.AllocateAndCompute(_chargingModel, _availablePower, _socTarget, _battery1);

    /// <summary>
    /// Benchmarks allocating and computing charging time for a dual charging point with one car.
    /// </summary>
    [Benchmark]
    public void DualPointOneCar() =>
        _ = _dualChargingPoint.AllocateAndCompute(_chargingModel, _availablePower, _socTarget, _battery1);

    /// <summary>
    /// Benchmarks allocating and computing charging time for a dual charging point with two cars.
    /// </summary>
    [Benchmark]
    public void DualPointTwoCars() =>
        _ = _dualChargingPoint.AllocateAndCompute(_chargingModel, _availablePower, _socTarget, _socTarget, _battery1, _battery2);
}