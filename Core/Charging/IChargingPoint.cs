namespace Core.Charging;

using Core.Shared;

public interface IChargingPoint
{
    /// <summary>
    /// Get a list of the compatible sockets for this charging point, no duplicates.
    /// </summary>
    /// <returns>Get a list of the compatible sockets for the charging point, no duplicates.</returns>
    List<Socket> GetSockets();
}
