using Engine;
using Core.Spawning;
using Core.Shared;
using Core.Routing;
using Engine.Parsers;
using Engine.Grid;

namespace Headless;

public static class Program
{
    public static async Task Main()
    {
        var polygons = PolygonParser.Parse(
            File.ReadAllText("../data/denmark.polygon.json"));

        var grid = Polygooner.GenerateGrid(0.1, polygons);

        // Print the grid to the console
        foreach (var row in grid.SpawnableCells.AsEnumerable().Reverse())
        {
            foreach (var cell in row)
            {
                Console.Write(cell.Spawnable ? "1 " : "0 ");
            }
        }
    }
}


