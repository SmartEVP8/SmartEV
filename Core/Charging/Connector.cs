namespace Core.Charging;

using Core.Shared;

/// <summary>
/// The connector represents a single cable/connection of a charging point.
/// </summary>
/// <param name="socket">The type of socket that the connector is compatible with.</param>
public struct Connector(Socket socket)
{
    /// <summary>
    /// Gets the total number of PowerKW capable of being delivered by this connector type.
    /// </summary>
    public readonly ushort PowerKW => Socket.PowerKW();

    /// <summary>
    /// Gets the Socket type of the connection.
    /// </summary>
    /// <returns>The socket type.</returns>
    public readonly Socket GetSocket() => Socket;

    private Socket Socket { get; set; } = socket;
}
