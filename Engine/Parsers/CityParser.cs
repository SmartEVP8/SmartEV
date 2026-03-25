namespace Engine.Parsers;

using System.Globalization;
using Core.Shared;
using Engine.Spawning;

/// <summary>
/// Parses city data from a CSV file and creates a list of City objects.
/// </summary>
public static class CityParser
{
    /// <summary>
    /// Parses city data from a CSV file and creates a list of City objects.
    /// </summary>
    /// <param name="csvPath">The path to the CSV file containing city data.</param>
    /// <returns>A list of City objects.</returns>
    public static List<City> Parse(FileInfo csvPath)
    {
        return [.. File.ReadLines(csvPath.FullName)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = line.Split(',');
                var name = parts[0];
                var population = int.Parse(parts[1]);
                var longitude = double.Parse(parts[2], CultureInfo.InvariantCulture);
                var latitude = double.Parse(parts[3], CultureInfo.InvariantCulture);
                return new City(name, new Position(longitude, latitude), population);
            })];
    }
}
