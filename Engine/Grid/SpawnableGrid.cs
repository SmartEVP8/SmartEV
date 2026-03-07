using Core.Shared;

public class SpawnableGrid(List<List<SpawnableGridCells>> spawnableCells)
{
    public readonly List<List<SpawnableGridCells>> SpawnableCells = spawnableCells;
}

public class SpawnableGridCells (float spawnChance, Position midpoint, List<(string CityName, float CitySpawnChance)> CityChances)
{
    public readonly float spawnChance = spawnChance;
    public List<(string CityName, float CityDestChance)> CityChances = CityChances;
    public readonly Position midpoint = midpoint;
}
