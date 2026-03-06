using Engine;
using Core.Spawning;
using Core.Shared;
using Core.Routing;

namespace Headless;

public static class Program
{
    public static async Task Main()
    {
        var router = new OSRMRouter("../data/output.osrm");
        //Read cities from ../CityInfo.csv
        var cityinfo = File.ReadAllLines("../CityInfo.csv").Skip(1).Select(line =>
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
        var calculateJourney = new CalculateJourney();
        var gridMatrix = calculateJourney.CalculateDestChance(new List<Position> { new (12.5683, 55.6761), new (9.4028, 56.4515) }, 1f, cityinfo, router);
        foreach (var grid in gridMatrix)
        {
            Console.WriteLine($"Grid {grid.Item1}:");
            foreach (var city in grid.Item2)
            {
                Console.WriteLine($"  {city.Item1}: {city.Item2}");
            }
            Console.WriteLine("Sum of spawn chances: " + grid.Item2.Sum(c => c.Item2));
        }
    }
}


