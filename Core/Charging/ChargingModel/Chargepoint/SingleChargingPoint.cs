namespace Core.Charging.ChargingModel.Chargepoint;

using System.Collections.Immutable;
using Core.Shared;

/// <summary>
/// A charging point with a single connector; supports one vehicle at a time.
/// </summary>
public readonly struct SingleChargingPoint(Connectors connectors) : ISingleChargingPoint
{
    private readonly Connectors _connectors = connectors;
    private readonly ImmutableArray<Socket> _sockets = [.. connectors.Sockets];

    /// <inheritdoc/>
    public ImmutableArray<Socket> GetSockets() => _sockets;

    /// <inheritdoc/>
    public double GetPowerOutput(double maxKW, double soc)
    {
        var cap = Math.Min(maxKW, _connectors.ActivePowerKW);
        return cap * ChargingCurve.PowerFraction(soc);
    }

    /// <inheritdoc/>
    public bool CanConnect(Socket socket) => _connectors.Supports(socket) && _connectors.IsFree;

    /// <inheritdoc/>
    public bool TryConnect(Socket socket)
    {
        if (!_connectors.IsFree || !_connectors.Supports(socket))
            return false;

        _connectors.Activate(_connectors.GetConnectorFor(socket));
        return true;
    }

    /// <inheritdoc/>
    public void Disconnect() => _connectors.Deactivate();
}