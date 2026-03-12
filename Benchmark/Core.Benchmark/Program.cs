namespace Headless;

using Core.Charging;
using Core.Routing;
using Core.Shared;
using Engine.Grid;
using Engine.Parsers;
using Parquet.Serialization;

public static class Program
{
    public static async Task Main()
    {
        var polygons = PolygonParser.Parse(
            File.ReadAllText("../../data/denmark.polygon.json"));

        var grid = Polygooner.GenerateGrid(0.1, polygons);

        // Print the grid to the console
        foreach (var row in grid.Cells.AsEnumerable().Reverse())
        {
            foreach (var cell in row)
            {
                Console.Write(cell.Spawnable ? "1 " : "0 ");
            }
        }

        var path = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");

        using var router = new OSRMRouter(path);

        var stations = new List<Station>();
        for (ushort i = 0; i < 250; i++)
        {
            stations.Add(new Station(
                id: i,
                name: $"Station {i}",
                address: string.Empty,
                position: new Position(9.9217 + (i * 0.01), 57.0488 + (i * 0.01)),
                chargers: [],
                price: 3.0f,
                random: new Random(i)));
        }

        router.InitStations(stations);

        var evCoords = new (double Lon, double Lat)[]
        {
            (9.9200, 57.0400),
            (9.9300, 57.0500),
            (9.9400, 57.0600),
        };

        var evCoordsFlat = evCoords.SelectMany(e => new[] { e.Lon, e.Lat }).ToArray();
        var stationCoordsFlat = stations.SelectMany(s => new[] { s.Position.Longitude, s.Position.Latitude }).ToArray();

        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "points_to_points.parquet");

        for (var i = 0; i < 3; i++)
        {
            var (durations, distances) = router.QueryPointsToPoints(evCoordsFlat, stationCoordsFlat);
            var rows = durations.Zip(distances, (dur, dist) => new Routing { Duration = dur, Distance = dist }).ToList();
            var options = new ParquetSerializerOptions { Append = i > 0 };
            await ParquetSerializer.SerializeAsync(rows, outputPath, options);
        }

        Console.WriteLine($"\nWritten: {outputPath}");

        IList<Routing> data = await ParquetSerializer.DeserializeAsync<Routing>(outputPath);
        Console.WriteLine($"Read {data.Count} rows from points_to_points.parquet");
        for (var i = 0; i < 15; i++)
            Console.WriteLine($"  Row {i}: duration={data[i].Duration}s distance={data[i].Distance}m");
    }
}