using Core.Shared;

public class SpawnableGrid(List<List<SpawnableGridCells>> spawnableCells)
{
    public readonly List<List<SpawnableGridCells>> SpawnableCells = spawnableCells;
}

public class SpawnableGridCells (float spawnChance, Position midpoint, List<(string CityName, float CitySpawnChance, float DestChance)> CityChances)
{
    public float spawnChance = spawnChance;
    public List<(string CityName, float DistToCity, float DestChance)> CityInfo = CityChances;
    public readonly Position midpoint = midpoint;
}
