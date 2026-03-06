namespace Simulation;

using Engine.Parsers;
using Engine.Grid;

public static class Program
{
    public static async Task Main()
    {
        var polygons = PolygonParser.Parse(
            File.ReadAllText("../data/denmark.polygon.json"));

        var grid = Polygooner.GenerateGrid(0.09, polygons);

        for (var i = grid.Length - 1; i >= 0; i--)
        {
            Console.WriteLine("[" + string.Join(" ", grid[i].Select(v => v == true ? 1 : 0)) + " ]");
        }
    }
}


