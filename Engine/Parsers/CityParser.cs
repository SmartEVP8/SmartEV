namespace Engine.Parsers;

using System.Globalization;
using Core.Shared;
using Engine.Spawning;

public static class CityParser
{
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
