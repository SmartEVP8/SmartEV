namespace Engine.Routing;

/// <summary>
/// Interface for a router that uses the OSRM API to calculate routes and durations.
/// </summary>
public interface IOSRMRouter :
    IMatrixRouter,
    IPointToPointRouter,
    IDestinationRouter,
    IMultiStationRouter,
    IDisposable
{
}
