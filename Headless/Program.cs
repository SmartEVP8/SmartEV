namespace Headless;

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
            File.ReadAllText("../data/denmark.polygon.json"));

        var grid = Polygooner.GenerateGrid(0.1, polygons);

        foreach (var gridRow in grid.SpawnableCells.AsEnumerable().Reverse())
        {
            foreach (var cell in gridRow)
                Console.Write(cell.Spawnable ? "1 " : "0 ");
        }

        var path = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");

        using var router = new OSRMRouter(path);

        var srcCoords = new (double Lon, double Lat)[]
        {
            (12.5683, 55.6761), // Copenhagen
            (9.9217,  57.0488), // Aalborg
            (10.2039, 56.1629), // Aarhus
        };

        var dstCoords = new (double Lon, double Lat)[]
        {
            (12.5683, 55.6761), // Copenhagen
            (10.2039, 56.1629), // Aarhus
            (9.9217,  57.0488), // Aalborg
        };

        var srcPos = srcCoords.SelectMany(e => new[] { e.Lon, e.Lat }).ToArray();
        var dstPos = dstCoords.SelectMany(e => new[] { e.Lon, e.Lat }).ToArray();

        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "parquetExample.parquet");

        for (var i = 0; i < 3; i++)
        {
            var (durations, distances) = router.QueryPointsToPoints(srcPos, srcCoords.Length, dstPos, dstCoords.Length);
            var rows = durations.Zip(distances, (dur, dist) => new Routing { Duration = dur, Distance = dist }).ToList();
            var options = new ParquetSerializerOptions { Append = i > 0 };
            await ParquetSerializer.SerializeAsync(rows, outputPath, options);
        }

        Console.WriteLine($"\nWritten: {outputPath}");

        IList<Routing> data = await ParquetSerializer.DeserializeAsync<Routing>(outputPath);
        for (var i = 0; i < data.Count; i++)
            Console.WriteLine($"  Row {i}: duration={data[i].Duration}s distance={data[i].Distance}m");
    }
}