using Core.Routing;
namespace Engine.Test;

using Engine;
using Core.Spawning;
using Core.Shared;

public class CalculateJourneyTest
{
    [Fact]
    public void TestCalculateSpawn()
    {
        var router = new OSRMRouter("../../../../../data/output.osrm");
        var calculateJourney = new CalculateJourney();
        var gridMatrix = calculateJourney.CalculateDestChance(new List<Position> { new (12.5683, 55.6761), new (9.4028, 56.4515) }, 1f, cities, router);
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
