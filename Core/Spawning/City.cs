namespace Core.Spawning;

using Core.Shared;

public class City(string name, float spawnChance, Position position)
{
    public readonly string name = name;
    public readonly float spawnChance = spawnChance;
    public readonly Position position = position;
}
