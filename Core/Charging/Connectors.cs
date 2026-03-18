namespace Core.Charging;

using Core.Shared;

/// <summary>
/// The Connectors struct represents a collection of connectors available at a charging point, along with the currently active connector (if any). 
/// </summary>
/// <param name="connectors">The initial set of connectors available at the charging point.</param>
public struct Connectors(IEnumerable<Connector> connectors)
{
    private readonly List<Connector> _connectors = [.. connectors];
    private Connector? _activeConnector;

    /// <summary>
    /// Gets the currently active connector, if any. Returns null if no connector is active.
    /// </summary> 
    /// <returns>The active connector or null if no connector is active.</returns>
    public readonly Connector? ActiveConnector => _activeConnector;

    /// <summary>
    /// Activates the specified connector, marking it as the currently active connector.  
    /// </summary>
    /// <param name="connector">The connector to activate.</param>
    /// <exception cref="InvalidOperationException">Thrown when the connector does not belong to this set.</exception>
    public void Activate(Connector connector)
    {
        if (!_connectors.Contains(connector))
            throw new InvalidOperationException("Connector does not belong to this set.");

        _activeConnector = connector;
    }

    /// <summary>
    /// Deactivates the currently active connector, if any, marking the charging point as free. If no connector is active, this method has no effect.
    /// </summary>
    public void Deactivate() => _activeConnector = null;

    /// <summary>
    /// Gets the total power in kilowatts (kW) currently being delivered by the active connector. If no connector is active, this returns 0 kW.
    /// </summary>
    public readonly ushort ActivePowerKW => _activeConnector?.PowerKW ?? 0;

    /// <summary>
    /// Gets a read-only list of the socket types supported by the connectors in this set. The list contains distinct socket types, even if multiple connectors support the same type.
    /// </summary>
    public readonly IReadOnlyList<Socket> Sockets => [.. _connectors.Select(c => c.Socket).Distinct()];

    /// <summary>
    /// Gets a value indicating whether no connector is currently active.
    /// </summary>
    public readonly bool IsFree => _activeConnector is null;

    /// <summary>
    /// Determines whether this connector set supports the specified socket type.
    /// </summary>
    /// <param name="socket">The socket type to check for support.</param>
    /// <returns>True if a connector supporting the socket type exists; otherwise, false.</returns>
    public readonly bool Supports(Socket socket) => _connectors.Any(c => c.Socket == socket);

    /// <summary>
    /// Gets the connector that supports the specified socket type.
    /// </summary>
    /// <param name="socket">The socket type to retrieve the connector for.</param>
    /// <returns>The connector supporting the specified socket type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no connector supports the specified socket type.</exception>
    public readonly Connector GetConnectorFor(Socket socket) => _connectors.First(c => c.Socket == socket);

    /// <summary>
    /// Creates a shallow copy of this connector set with new Connector instances for each socket type.
    /// </summary>
    /// <returns>A new Connectors instance containing copies of the connectors in this set.</returns>
    public readonly Connectors Copy() => new([.. _connectors.Select(c => new Connector(c.Socket))]);
}
