namespace Engine.Test;

using Engine;
using Core.Spawning;
using Core.Shared;

public class CalculateJourneyTest
{
    [Fact]
    public void TestCalculateSpawnFromKBHCenter()
    {
        var cities = new List<City>
        {
            new City("Frederikshavn", new Position(57.44f, 10.54f), 23000),
            new City("Viborg", new Position(56.45f, 9.40f), 96000),
            new City("Frederiksberg", new Position(55.68f, 12.53f), 105000),
            new City("København", new Position(55.68f, 12.57f), 800000),
            new City("Havdrup", new Position(55.50f, 12.20f), 5000),
        };
        var calculateJourney = new CalculateJourney();
        var result = calculateJourney.CalculateSpawn(new Position(55.6761f, 12.5683f), 0.5f, cities);
        Assert.NotEmpty(result);
        Assert.Contains(result, city => city.Name == "København" && Math.Abs(city.SpawnChance - 0.938209772) < 1e-6);
        Assert.True(Math.Abs(result.Sum(city => city.SpawnChance) - 1) < 1e-6);
    }

    [Fact]
    public void TestCalculateSpawnFromKBHCenterWithHigherScaler()
    {
        var cities = new List<City>
        {
            new City("Frederikshavn", new Position(57.44f, 10.54f), 23000),
            new City("Viborg", new Position(56.45f, 9.40f), 96000),
            new City("Frederiksberg", new Position(55.68f, 12.53f), 105000),
            new City("København", new Position(55.68f, 12.57f), 800000),
            new City("Havdrup", new Position(55.50f, 12.20f), 5000),
        };
        var calculateJourney = new CalculateJourney();
        var result = calculateJourney.CalculateSpawn(new Position(55.6761f, 12.5683f), 1f, cities);
        Assert.NotEmpty(result);
        Assert.Contains(result, city => city.Name == "København" && Math.Abs(city.SpawnChance - 0.977446973) < 1e-6);
        Assert.True(Math.Abs(result.Sum(city => city.SpawnChance) - 1) < 1e-6);
    }
}
