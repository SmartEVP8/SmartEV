namespace Core.Charging;

using Core.Shared;

/// <summary>
/// The connector represents a single cable/connection of a charging point.
/// </summary>
/// <param name="socket">The type of socket that the connector is compatible with.</param>
public readonly struct Connector(Socket socket)
{
    /// <summary>
    /// Gets the socket type of the connection.
    /// </summary>
    public Socket Socket { get; } = socket;

    /// <summary>
    /// Gets the maximum power in kilowatts capable of being delivered by this connector type.
    /// </summary>
    public ushort PowerKW => Socket.PowerKW();
}