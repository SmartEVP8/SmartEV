
namespace Headless;
using Engine;
using Engine.Grid;
using Core.Spawning;
using Core.Shared;
using Core.Routing;
using Engine.Parsers;

public static class fun
{
    public static async Task Main()
    {
        var router = new OSRMRouter("../data/output.osrm");
        //Read cities from ../CityInfo.csv
        var cityinfo = File.ReadAllLines("../data/CityInfo.csv").Skip(1).Select(line =>
        {
            var parts = line.Split(',');
            var name = parts[0];
            var longitude = double.Parse(parts[2]);
            var latitude = double.Parse(parts[3]);
            var population = int.Parse(parts[1]);
            return new City(name, new Position(longitude, latitude), population);
        }).ToList();
        if (cityinfo.Count == 0)
        {
            Console.WriteLine("No cities found in CityInfo.csv");
            return;
        }
        var spawnableGrid = new SpawnableGrid([]);
        var polygons = PolygonParser.Parse(
            File.ReadAllText("../data/denmark.polygon.json"));
        var grids = Polygooner.GenerateGrid(0.1, polygons);


        var calculateJourney = new CalculateJourney();
        var gridMatrix = calculateJourney.CalculateDestChance(spawnableGrid, 1f, cityinfo, router);

    }
}
