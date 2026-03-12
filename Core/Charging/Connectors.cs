namespace Core.Charging;

using Core.Shared;

public struct Connectors(IEnumerable<Connector> connectors)
{
    private readonly List<Connector> _connectors = [.. connectors];
    private Connector? _activeConnector;

    public readonly Connector? ActiveConnector => _activeConnector;

    public void Activate(Connector connector)
    {
        if (!_connectors.Contains(connector))
            throw new InvalidOperationException("Connector does not belong to this set.");

        _activeConnector = connector;
    }

    public void Deactivate() => _activeConnector = null;

    public ushort ActivePowerKW => _activeConnector?.PowerKW ?? 0;

    public readonly IReadOnlyList<Socket> Sockets => [.. _connectors.Select(c => c.GetSocket()).Distinct()];

    public bool IsFree => _activeConnector is null;

    public bool Supports(Socket socket) => _connectors.Any(c => c.GetSocket() == socket);

    public readonly Connector GetConnectorFor(Socket socket) => _connectors.First(c => c.GetSocket() == socket);
}
