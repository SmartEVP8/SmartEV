namespace Core.Charging;

using Core.Shared;

public class Station(ushort id,
               string name,
               string address,
               Position position,
               List<Charger>? chargers)
{
    private readonly ushort _id = id;
    private readonly string _name = name;
    private readonly string _address = address;
    public readonly Position Position = position;
    private readonly List<Charger>? _chargers = chargers;
}
