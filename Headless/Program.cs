namespace Headless;

using Core.Routing;
using Core.Models;
using Core.Services;
using Engine.Grid;
using Engine.Parsers;
using Parquet;

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

        var srcFlat = srcCoords.SelectMany(e => new[] { e.Lon, e.Lat }).ToArray();
        var dstFlat = dstCoords.SelectMany(e => new[] { e.Lon, e.Lat }).ToArray();

        var (durations, distances) = router.QueryPointsToPoints(srcFlat, srcCoords.Length, dstFlat, dstCoords.Length);

        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "points_to_points.parquet");

        await using (var writer = await Writer.CreateAsync(outputPath, RoutingRow.Schema))
        {
            // RowGroup 0
            await writer.AppendAsync(RoutingRow.ToColumns(durations, distances));
            var (durations2, distances2) = router.QueryPointsToPoints(dstFlat, dstCoords.Length, srcFlat, srcCoords.Length);

            // RowGroup 1
            await writer.AppendAsync(RoutingRow.ToColumns(durations2, distances2));
        }

        Console.WriteLine($"\nWritten: {outputPath}");
        using var readStream = File.OpenRead(outputPath);
        using var reader = await ParquetReader.CreateAsync(readStream);

        Console.WriteLine();
        for (var g = 0; g < reader.RowGroupCount; g++)
        {
            using var rowGroup = reader.OpenRowGroupReader(g);
            var readDurs = (float[])(await rowGroup.ReadColumnAsync(RoutingRow.Schema.DataFields[0])).Data;
            var readDists = (float[])(await rowGroup.ReadColumnAsync(RoutingRow.Schema.DataFields[1])).Data;

            for (var i = 0; i < readDurs.Length; i++)
                Console.WriteLine($"  [{g}] Row {i}: duration={readDurs[i]}s distance={readDists[i]}m");
        }
    }
}