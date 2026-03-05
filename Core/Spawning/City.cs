namespace Core.Spawning;

using Core.Shared;

public class City(string name, Position position, int population, float spawnChance = 0.0f)
{
    public readonly string Name = name;

    public readonly Position Position = position;

    public readonly int Population = population;

    public float SpawnChance = spawnChance;
}
