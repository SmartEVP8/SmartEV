using Core.Spawning;
using Engine;
namespace Headless;

using Core.Charging;
using Core.Routing;
using Core.Services;
using Core.Shared;

using Engine.Parsers;
using Engine.Grid;
using System.Security.Cryptography;

public static class Program
{
    public static async Task Main()
    {
        var router = new OSRMRouter("../data/osrm/output.osrm");
        // Read cities from ../CityInfo.csv
        var cityinfo = File.ReadAllLines("../data/CityInfo.csv").Skip(1).Select(line =>
        {
            var parts = line.Split(',');
            var name = parts[0];
            var longitude = double.Parse(parts[2]);
            var latitude = double.Parse(parts[3]);
            var population = int.Parse(parts[1]);
            return new City(name, new Position(longitude: longitude, latitude), population);
        }).ToList();
        if (cityinfo.Count == 0)
        {
            Console.WriteLine("No cities found in CityInfo.csv");
            return;
        }
        var polygons = PolygonParser.Parse(File.ReadAllText("../data/denmark.polygon.json"));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        foreach (var row in grid.Cells.AsEnumerable())
        {
            foreach (var cell in row)
            {
                Console.Write(cell.Spawnable ? "1 " : "0 ");
            }
            Console.WriteLine();
        }

        var calculateJourney = new CalculateJourney();
        var distanceMatrix = calculateJourney.CalculateDistance(grid,  cityinfo, router);
        var scaler = 0.5f; // Adjust this value to control the influence of population on spawn chances
        var destChanceGrid = calculateJourney.CalculateChances(distanceMatrix, cityinfo, scaler);
        var spawnChanceGrid = calculateJourney.CalculateSpawnRate(destChanceGrid);

        // Print the spawn chances for each cell in the grid
        for (ushort i = 0; i < spawnChanceGrid.SpawnableCells.Count; i++)
        {
            for (ushort j = 0; j < spawnChanceGrid.SpawnableCells[i].Count; j++)
            {
                var cell = distanceMatrix.SpawnableCells[i][j];
                Console.WriteLine($"Cell ({i}, {j}) at {cell.midpoint}:");
                Console.WriteLine($"  Spawn Chance: {cell.spawnChance}");
            }
        }
    }
}
