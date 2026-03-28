namespace Core.Vehicles;

using Core.Shared;

/// <summary>
/// Represents the battery of an electric vehicle.
/// </summary>
/// <param name="capacity">The capacity of the battery.</param>
/// <param name="maxChargeRate">The maximum charge rate of the battery.</param>
/// <param name="stateOfCharge">The current state of charge of the battery.</param>
/// <param name="socket">The socket type of the battery.</param>
public class Battery(ushort capacity, ushort maxChargeRate, float stateOfCharge, Socket socket)
{
    /// <summary>Gets the capacity of the battery.</summary>
    public ushort MaxCapacityKWh { get; } = capacity;

    /// <summary>Gets the maximum charge rate of the battery.</summary>
    public ushort MaxChargeRateKW { get; } = maxChargeRate;

    /// <summary>Gets or sets the current state of charge of the battery.</summary>
    public float StateOfCharge { get; set; } = stateOfCharge;

    /// <summary>Gets the socket type of the battery.</summary>
    public Socket Socket { get; } = socket;
}
