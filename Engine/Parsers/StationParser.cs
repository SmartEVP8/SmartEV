namespace Engine.Parsers;

using System.Text.Json;
using Core.Shared;
using Serilog;

/// <summary>
/// JSON parser for extracting station coordinates and converting them to lists of Position objects.
/// </summary>
public static class StationParser
{
    private record StationEntry(string Name, double Latitude, double Longitude);

    /// <summary>
    /// Parses a JSON string containing station data and extracts the coordinates, converting them into a list of Position objects.
    /// </summary>
    /// <param name="json">The JSON string containing station data.</param>
    /// <returns>A list of Position objects representing the station coordinates.</returns>
    /// <exception cref="Exception">Thrown when the JSON is invalid or cannot be parsed.</exception>
    public static List<Position> Parse(string json)
    {
        var stations = JsonSerializer.Deserialize<List<StationEntry>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (stations == null)
        {
            Log.Error("Failed to deserialize station JSON from string: {@Json}", json);
            throw new Exception("Failed to deserialize station JSON.");
        }

        return [.. stations.Select(s => new Position(s.Longitude, s.Latitude))];
    }
}
