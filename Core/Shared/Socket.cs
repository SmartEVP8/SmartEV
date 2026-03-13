namespace Core.Shared;

/// <summary>
/// A socket represents the type of connector used for charging an electric vehicle.
/// Different vehicles may require different types of sockets, and charging stations may offer multiple socket types to accommodate a variety of vehicles.
/// </summary>
public enum Socket : byte
{
    CHADEMO,
    CCS2,
    Type2,
    Tesla_ModelSX,
    Tesla_Model3
}

public static class SocketExtensions
{
    /// <summary>
    /// Returns a string representation of the Socket enum value.
    /// </summary>
    /// <param name="socket">The type of socket.</param>
    /// <returns>A string representation of the enum.</returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the socket value is not defined on the enum.</exception>
    public static string ToString(this Socket socket)
    {
        return socket switch
        {
            Socket.CHADEMO => "CHAdeMO",
            Socket.CCS2 => "CCS2",
            Socket.Type2 => "Type 2",
            Socket.Tesla_ModelSX => "Tesla Model S/X",
            Socket.Tesla_Model3 => "Tesla Model 3/Y",
            _ => throw new ArgumentOutOfRangeException(nameof(socket), socket, null)
        };
    }

    /// <summary>
    /// Get the associated power output in kilowatts (kW) for a given socket type.
    /// </summary>
    /// <param name="socket">The type of socket.</param>
    /// <returns>Total powerKW for a given socket.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the scoket value is not defined on the enum.</exception>
    public static ushort PowerKW(this Socket socket)
    {
        // These numbers are placeholders for now
        return socket switch
        {
            Socket.CHADEMO => 50,
            Socket.CCS2 => 350,
            Socket.Type2 => 22,
            Socket.Tesla_ModelSX => 250,
            Socket.Tesla_Model3 => 250,
            _ => throw new ArgumentOutOfRangeException(nameof(socket), socket, null)
        };
    }
}
