namespace Engine.Spawning;

using Core.Shared;

/// <summary>
/// Represents a city where EV's can spawn.
/// </summary>
/// <param name="name">The name of the city.</param>
/// <param name="position">The position of the city.</param>
/// <param name="population">The population of the city.</param>
/// <param name="spawnChance">The chance of an EV spawning in the city.</param>
public class City(string name, Position position, int population, float spawnChance = 0.0f)
{
    /// <summary>Gets the id of the city.</summary>
    public int Id { get; }

    /// <summary>Gets the name of the city.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the position of the city.</summary>
    public Position Position { get; } = position;

    /// <summary>Gets the population of the city.</summary>
    public int Population { get; } = population;

    /// <summary>Gets the spawn chance of the city.</summary>
    public float SpawnChance { get; } = spawnChance;
}
