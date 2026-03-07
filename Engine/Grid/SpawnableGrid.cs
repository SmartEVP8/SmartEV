using Core.Shared;
using Core.Spawning;

public class SpawnableGrid(List<List<SpawnableCell>> spawnableCells)
{
    public readonly List<List<SpawnableCell>> SpawnableCells = spawnableCells;
}

public class SpawnableCell(float spawnChance, Position Centerpoint, List<CityInfo> CityChances)
{
    public readonly List<CityInfo> CityInfo = CityChances;
    public readonly Position Centerpoint = Centerpoint;
    public AliasSampler? CitySampler = null;
}

public struct CityInfo(string CityName, float DistToCity, float Population)
{
    public readonly string CityName = CityName;
    public readonly float DistToCity = DistToCity;
    public readonly float Population = Population;
}
