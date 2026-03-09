namespace Headless;

using Core.Spawning;
using Core.Routing;
using Core.Shared;

using Engine;
using Engine.Parsers;
using Engine.Grid;

public static class Program
{
    public static async Task Main()
    {
        var router = new OSRMRouter("../data/osrm/output.osrm");
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

        var pipeline = new JourneyPipeline(grid, cityinfo, router);
        var samplers = pipeline.Compute(1.0f);

        if (samplers == null)
        {
            Console.WriteLine("Samplers were not created successfully.");
            return;
        }

        var probabilities = samplers.SourceSampler.GetProbabilities();

        probabilities.ForEach(val => Console.WriteLine(val + " "));


        var lol = grid.Cells.SelectMany(c => c).Count(c => c.Spawnable == true);
        Console.WriteLine($"Total spawnable cells: {lol}");
        Console.WriteLine($"Total probabilities samples: {probabilities.Count}");
    }
}
