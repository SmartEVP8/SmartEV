namespace Core.Charging;

/// <summary>
/// The Connectors struct represents a collection of connectors available at a charging point, along with the currently active connector (if any).
/// </summary>
/// <param name="connectors">The initial set of connectors available at the charging point.</param>
public struct Connectors((Connector Left, Connector Right) connectors)
{
    /// <summary>
    /// The connectors available at the charging point, along with their current status (free or active).
    /// </summary>
    public (Connector Left, Connector Right) AttachedConnectors = connectors;
}

/// <summary>
/// Represents a single connector at a charging point, which can be either free or active (connected to a vehicle).
/// </summary>
/// <param name="powerKW">The amout of KW it can charge with.</param>
public struct Connector(ushort powerKW)
{
    /// <summary>
    /// Indicates whether the connector is currently free (not connected to a vehicle) or active (connected to a vehicle).
    /// </summary>
    public bool IsFree = true;

    /// <summary>
    /// Updates the connector to indicate that its not free.
    /// </summary>
    public void Activate() => IsFree = false;

    /// <summary>
    /// Updates the connector to indicate that its free.
    /// </summary>
    public void Deactivate() => IsFree = true;

    /// <summary>
    /// Gets the power in kilowatts that this connector can deliver when active.
    /// </summary>
    public readonly ushort PowerKW => powerKW;
}
