namespace Engine.Parsers;

using System.Text.Json;
using Core.Shared;

public class GeoJson
{
    public string Type { get; set; } = "";
    public List<Feature> Features { get; set; } = [];
}

public class Feature
{
    public string Type { get; set; } = "";
    public Geometry Geometry { get; set; } = new();
}

public class Geometry
{
    public string Type { get; set; } = "";
    public List<List<List<double>>> Coordinates { get; set; } = [];
}

public static class PolygonParser
{
    public static List<List<Position>> Parse(string json)
    {
        var geo = JsonSerializer.Deserialize<GeoJson>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })
            ?? throw new Exception("Invalid GeoJSON");

        var polygons = new List<List<Position>>();

        foreach (var feature in geo.Features)
        {
            if (feature.Geometry?.Coordinates == null || feature.Geometry.Coordinates.Count == 0)
                continue;

            var polygonPoints = new List<Position>();
            var outerRing = feature.Geometry.Coordinates[0];

            foreach (var coord in outerRing)
            {
                if (coord.Count < 2)
                    continue;

                var lon = coord[0];
                var lat = coord[1];

                polygonPoints.Add(new Position(lat, lon));
            }

            polygons.Add(polygonPoints);
        }

        return polygons;
    }
}
