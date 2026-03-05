namespace Core.Spawning;

using Core.Shared;

public class City(string name, float spawnChance, Position position, int population)
{
    public readonly int Id;
    public readonly string Name = name;
    public readonly float SpawnChance = spawnChance;
    public readonly int Population = population;
    public readonly Position Position = position;
}
