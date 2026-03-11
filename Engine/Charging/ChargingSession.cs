namespace Engine.Charging;

using Core.Charging;
using Core.Vehicles;

/// <summary>
/// Represents an active charging session at a charging point, managing the allocation of power
/// to one or two electric vehicles and tracking their state of charge targets.
/// </summary>
/// <param name="allocator"> The charging allocator to use for computing charging times. </param>
/// <param name="chargingPoint"> The charging point where the session is taking place. </param>
/// <param name="availablePower"> The total power available for allocation. </param>
public class ChargingSession(ChargingAllocator allocator, IChargingPoint chargingPoint, double availablePower)
{
    private EV? _car1;
    private EV? _car2;

    private double _car1SocTarget;
    private double _car2SocTarget;

    /// <summary>
    /// Starts charging an electric vehicle at the charging point. If there is already a vehicle
    /// being charged, the new vehicle will be added to the session and the charging times
    /// will be recalculated for both vehicles.
    /// </summary>
    /// <param name="car"> The electric vehicle to charge. </param>
    /// <param name="socTarget"> The target state of charge for the vehicle. </param>
    public void StartCharging(EV car, double socTarget)
    {
        if (_car1 is null)
            StartChargingFirstCar(car, socTarget);
        else
            StartChargingSecondCar(car, socTarget);
    }

    /// <summary>
    /// Stops charging the specified electric vehicle. If the vehicle is currently being charged,
    /// it will be removed from the session and the remaining vehicle (if any) will have its charging
    /// time recalculated based on the new available power.
    /// </summary>
    /// <param name="car"> The electric vehicle to stop charging. </param>
    public void StopCharging(EV car)
    {
        if (_car1 == car)
            StopChargingFirstCar();
        else if (_car2 == car)
            StopChargingSecondCar();
    }

    // TODO: Change the discards (_ = allocator.AllocateAndCompute(...)) to actually store the results
    // and use them to track the state of charge of the cars in the session.
    private void StartChargingFirstCar(EV car, double socTarget)
    {
        _car1 = car;
        _car1SocTarget = socTarget;
        _ = allocator.AllocateAndCompute(chargingPoint, availablePower, socTarget, 0.0, car.Battery);
    }

    private void StartChargingSecondCar(EV car, double socTarget)
    {
        _car2 = car;
        _ = allocator.AllocateAndCompute(chargingPoint, availablePower, _car1SocTarget, socTarget, _car1!.Battery, car.Battery);
    }

    private void StopChargingFirstCar()
    {
        _car1!.UpdateCharge((float)_car1SocTarget);
        _car1 = null;
        _car1SocTarget = 0.0;

        if (_car2 is not null)
        {
            _car1 = _car2;
            _car1SocTarget = _car2SocTarget;
            _car2 = null;
            _car2SocTarget = 0.0;
            _ = allocator.AllocateAndCompute(chargingPoint, availablePower, _car1SocTarget, 0.0, _car1.Battery);
        }
    }

    private void StopChargingSecondCar()
    {
        _car2!.UpdateCharge((float)_car2SocTarget);
        _car2 = null;
        _car2SocTarget = 0.0;

        if (_car1 is not null)
        {
            _ = allocator.AllocateAndCompute(chargingPoint, availablePower, _car1SocTarget, 0.0, _car1.Battery);
        }
    }
}