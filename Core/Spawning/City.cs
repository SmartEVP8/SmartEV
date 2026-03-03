namespace Core.Spawning;

using Core.Shared;

public class City(string name, float spawnChance, Position position)
{
    public readonly string Name = name;
    public readonly float SpawnChance = spawnChance;
    public readonly Position Position = position;
}
