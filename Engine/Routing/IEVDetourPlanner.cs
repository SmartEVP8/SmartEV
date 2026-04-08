
using Core.Charging;
using Core.Shared;
using Core.Vehicles;

public interface IEVDetourPlanner
{
    void Update(ref EV ev, Station station, Time currentTime);
}
