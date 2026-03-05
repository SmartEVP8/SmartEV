namespace Engine;
using Core.Shared;
using Core.Spawning;

public class CalculateJourney
{
    /// <summary>
    /// Calculates the spawn chances for cities based on their population and distance from center position of a grid.
    /// </summary> <param name="position">The center position of the grid.</param>
    /// <param name="scaler">A scaler to adjust the influence of population on spawn chance. Used to incentivise going to big cities or discourage.</param>
    /// <returns>A list of cities with their spawn chances for the given grid.</returns>
    /// <example>
    /// var calculateJourney = new CalculateJourney();
    /// var result = calculateJourney.Calculate(new Position(55.6761, 12.5683), 1f);
    /// result.cities are then:
    /// ...
    /// Frederikshavn 0.03%
    /// Viborg: 0.05%
    /// Frederiksberg: 4.692597%
    /// København: 75.6652%
    /// Havdrup: 0.02577%.
    /// ...
    /// <!---->
    /// Other example:
    /// var calculateJourney = new CalculateJourney();
    /// var result = calculateJourney.Calculate(new Position(55.6761, 12.5683), 0.5f);
    /// result.cities are then:
    /// Frederikshavn: 0.007489727%
    /// Viborg: 0.089010724%
    /// Frederiksberg: 5.2175224%
    /// København: 34.165975%
    /// Havdrup: 0.13564115%.
    /// </example>
    public List<City> Calculate(Position position, float scaler)
    {
        var cities = new List<City>();

        // Read city data from CSV file, gathered from https://www.dst.dk/da/, and create City objects with name, population and position.
        using (var reader = new StreamReader("CityInfo.csv"))
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line?.Split(',');
                if (values == null || values[1] == "Population" ) continue; // Skip header
                cities.Add(new City(values[0], 0.1f, new Position(float.Parse(values[2]), float.Parse(values[3])), int.Parse(values[1])));
            }
        }

        var diststuff = new List<(string, float)>();
        foreach (var city in cities)
        {
            // Calculate the distance between the city and the center position using the Haversine formula, and calculate a spawn chance based on the population and distance.
            // https://en.wikipedia.org/wiki/Haversine_formula
            var lat1 = position.Latitude * Math.PI / 180.0;
            var lon1 = position.Longitude * Math.PI / 180.0;
            var lat2 = city.Position.Latitude * Math.PI / 180.0;
            var lon2 = city.Position.Longitude * Math.PI / 180.0;

            var a = Math.Pow(Math.Sin((lat2 - lat1) / 2), 2) +
                       (Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Pow(Math.Sin((lon2 - lon1) / 2), 2));
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = (float)(6371 * c); // Earth's radius in kilometers

            // Calculate spawn chance based on population and distance, using the scaler to adjust the influence of population. The distance is raised to the power of 0.8 to reduce its influence on spawn chance, and the population is raised to the power of the scaler to adjust its influence.
            diststuff.Add((city.Name, (float)Math.Pow(city.Population, scaler) / (float)Math.Pow(distance, 0.8)));
        }

        // Normalize spawn chances so they sum to 1, and create a new list of cities with their spawn chances.
        var diffsum = diststuff.Sum(x => x.Item2);
        var result = new List<City>();
        foreach (var city in cities)
        {
            var dist = diststuff.First(x => x.Item1 == city.Name).Item2;

            // The spawn chance is the normalized value of the distance and population, so that the sum of all spawn chances is 1. This means that if a city has a spawn chance of 0.2, it has a 20% chance of being chosen as its destination when spawning a journey.
            result.Add(new City(city.Name, dist / diffsum, city.Position, city.Population));
        }

        return result;
    }
}
