namespace Engine.Parsers;

using System.Text.Json;
using Core.Shared;

/// <summary>
/// GeoJSON parser for extracting polygon coordinates and converting them to lists of Position objects.
/// </summary>
public class GeoJson
{
    /// <summary>
    /// Gets or sets the type of the GeoJSON object.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of features contained in the GeoJSON object, where each feature represents a geometric shape such as a polygon.
    /// </summary>
    public List<Feature> Features { get; set; } = [];
}

/// <summary>
/// Represents a feature in the GeoJSON object, which contains a geometry that defines the shape and coordinates of the feature.
/// </summary>
public class Feature
{
    /// <summary>
    /// Gets or sets the type of the feature.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the geometry of the feature.
    /// </summary>
    public Geometry Geometry { get; set; } = new();
}

/// <summary>
/// Represents the geometry of a feature in the GeoJSON object.
/// </summary>
public class Geometry
{
    /// <summary>
    /// Gets or sets the type of the geometry.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the coordinates of the geometry, which is a list of lists of lists of doubles representing the longitude and latitude of each point in the polygon.
    /// </summary>
    public List<List<List<double>>> Coordinates { get; set; } = [];
}

/// <summary>
/// Provides a method to parse a GeoJSON string and extract the polygon coordinates, converting them into lists of Position objects.
/// </summary>
public static class PolygonParser
{
    /// <summary>
    /// Parses a GeoJSON string to extract polygon coordinates and converts them into lists of Position objects, where each list represents a polygon defined by its vertices.
    /// </summary>
    /// <param name="json">The GeoJSON string to parse.</param>
    /// <returns>A list of polygons, where each polygon is represented as a list of Position objects.</returns>
    /// <exception cref="Exception">Thrown when the GeoJSON string is invalid.</exception>
    public static List<List<Position>> Parse(string json)
    {
        var geo = JsonSerializer.Deserialize<GeoJson>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            })
            ?? throw Log.Error(0, 0, new Exception("Invalid GeoJSON"));

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

                polygonPoints.Add(new Position(lon, lat));
            }

            polygons.Add(polygonPoints);
        }

        return polygons;
    }
}
