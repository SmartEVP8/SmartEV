
namespace Core.Charging;


using Core.Shared;

/// <summary>
/// DualChargingPoint represents a charging point with two connectors, allowing for two electric vehicles to charge simultaneously. Each connector can be of a different type, providing flexibility for various vehicle models and charging standards. The DualChargingPoint is designed to optimize the use of charging infrastructure by enabling multiple vehicles to charge at the same time, which can be particularly beneficial in high-traffic areas or for stations with limited space.
/// </summary>
public class DualChargingPoint(List<Connector> leftSide, List<Connector> rightSide) : IChargingPoint
{
    private List<Connector> _leftSide = leftSide;
    private List<Connector> _rightSide = rightSide;

    /// <inheritdoc/>
    public List<Socket> GetSockets()
    {
        var sockets = new List<Socket>();
        sockets.AddRange(_leftSide.Select(c => c.GetSocket()));
        sockets.AddRange(_rightSide.Select(c => c.GetSocket()));
        return sockets.Distinct().ToList();
    }
}


