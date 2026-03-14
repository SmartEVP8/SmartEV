namespace Engine.Spawning;

using Core.Shared;

public class City(string name, Position position, int population, float spawnChance = 0.0f)
{
    public readonly int Id;
    public readonly string Name = name;
    public readonly Position Position = position;
    public readonly int Population = population;
}
