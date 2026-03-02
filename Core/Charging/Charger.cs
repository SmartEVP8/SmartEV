namespace Core.Charging;

using Core.Shared;

public class Charger(int id, int PowerKW, Socket socket)
{
    public readonly int _id = id;
    public readonly int _powerKW = PowerKW;
    public readonly Socket _socket = socket;
}
