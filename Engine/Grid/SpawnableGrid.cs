using Core.Shared;

public class SpawnableGrid(List<List<SpawnableCells>> spawnableCells)
{
    public readonly List<List<SpawnableCells>> SpawnableCells = spawnableCells;
}

public class SpawnableCells(float spawnChance, Position midpoint, List<CityInfo> CityChances)
{
    public float spawnChance = spawnChance;
    public List<CityInfo> CityInfo = CityChances;
    public readonly Position midpoint = midpoint;
}

public struct CityInfo(string CityName, float DistToCity, float DestChance)
{
    public readonly string CityName = CityName;
    public readonly float DistToCity = DistToCity;
    public readonly float DestChance = DestChance;
}
