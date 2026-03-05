namespace Core.Charging;

using Core.Shared;

/// <summary>
/// SingleChargingPoint represents a charging point with a single connector, allowing for one electric vehicle to charge at a time.
/// </summary>
public readonly struct SingleChargingPoint : IChargingPoint
{
    private readonly List<Connector> _connectors;

    /// <inheritdoc/>
    public List<Socket> GetSockets() => _connectors.Select(c => c.GetSocket()).Distinct().ToList();
}
