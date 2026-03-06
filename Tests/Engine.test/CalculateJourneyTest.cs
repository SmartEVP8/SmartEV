namespace Engine.Test;

using Engine;
using Core.Spawning;
using Core.Shared;

public class CalculateJourneyTest
{
    [Fact]
    public void TestCalculateSpawn()
    {
        var cities = new List<City>
        {
            new City("Copenhagen", new Position(12.5683, 55.6761), 794128),
            new City("Frederiksberg", new Position(12.5218, 55.6729), 104305),
            new City("Viborg", new Position(9.4028, 56.4515), 96000),
            new City("Frederikshavn", new Position(10.5400, 57.4400), 23000),
            new City("Havdrup", new Position(12.2000, 55.5000), 5000),
        };
        var calculateJourney = new CalculateJourney();
        var gridMatrix = calculateJourney.CalculateSpawn(new List<Position> { new (12.5683, 55.6761), new (9.4028, 56.4515) }, 1f, cities);
        Assert.NotNull(gridMatrix);
        Assert.Equal(2, gridMatrix.Count);
        foreach (var grid in gridMatrix)
        {
            Assert.NotNull(grid.Item2);
            Assert.Equal(cities.Count, grid.Item2.Length);
            foreach (var city in grid.Item2)
            {
                Assert.False(string.IsNullOrEmpty(city.Item1));
                Assert.True(city.Item2 >= 0);
            }
        }
    }
}
