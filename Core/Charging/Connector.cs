using Core.Shared;
namespace Core.Charging;

/// <summary>
/// The Connectors struct represents a collection of connectors available at a charging point, along with the currently active connector (if any).
/// </summary>
/// <param name="connectors">The initial set of connectors available at the charging point.</param>
public struct Connectors((Connector Left, Connector Right) connectors)
{
    public (Connector Left, Connector Right) AllConnectors = connectors;
}

public struct Connector(ushort powerKW)
{
    public bool IsFree = true;

    public void Activate() => IsFree = false;
    public void Deactivate() => IsFree = true;
    public readonly ushort PowerKW => powerKW;
}
