namespace Engine.Routing;

using Core.Shared;
using Core.Charging;
using Core.Vehicles;

public interface IApplyNewPath
{
    Position ApplyNewPathToEV(ref EV ev, Station station, Time currentTime);
}

