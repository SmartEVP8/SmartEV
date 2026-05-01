namespace Engine.Spawning;

using System.Text.Json;

/// <summary>
/// Utility class for caching journey samplers to fike. This allows us to avoid expensive recomputation of samplers during development, while still allowing for dynamic sampling based on time of day.
/// </summary>
internal static class JourneySamplerCache
{
    private const string _cacheDir = "journeySamples";

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
    };

    /// <summary>
    /// Ensures the directory for caching journey samplers exists.
    /// </summary>
    public static void EnsureDirectory() => Directory.CreateDirectory(_cacheDir);

    /// <summary>
    /// Checks if a sampler for the given hour already exists.
    /// </summary>
    /// <param name="hour">The hour for which to check if a file already exist.</param>
    /// <param name="distanceScaler">The distance scaler used in the sampler, used to differentiate files when the scaler is changed.</param>
    /// <returns>Returns true if the file exist.</returns>
    public static bool Exists(uint hour, float distanceScaler)
    {
        var path = PathFor(hour, distanceScaler);
        return File.Exists(path) && new FileInfo(path).Length > 10;
    }

    /// <summary>
    /// Writes the given sampler DTO to disk for the specified hour.
    /// </summary>
    /// <param name="hour">The hour for which time the sampler was computed for.</param>
    /// <param name="journeyDTO">The sampler the should be written to file.</param>
    /// <param name="distanceScaler">The distance scaler used in the sampler, used to differentiate files when the scaler is changed.</param>
    public static void Write(uint hour, JourneySamplerDTO journeyDTO, float distanceScaler)
    {
        var json = JsonSerializer.Serialize(journeyDTO, _jsonOpts);
        if (json.Length < 10)
        {
            throw new InvalidOperationException(
                $"Serialised DTO for hour {hour} is empty: {json}");
        }

        File.WriteAllText(PathFor(hour, distanceScaler), json);
    }

    /// <summary>
    /// Reads the sampler from the specified hour.
    /// </summary>
    /// <param name="hour">The hour for which sampler is needed.</param>
    /// <param name="distanceScaler">The distance scaler used in the sampler, used to differentiate files when the scaler is changed.</param>
    /// <returns>Returns a JourneySamplerDTO.</returns>
    public static JourneySamplerDTO Read(uint hour, float distanceScaler)
    {
        var json = File.ReadAllText(PathFor(hour, distanceScaler));
        return JsonSerializer.Deserialize<JourneySamplerDTO>(json, _jsonOpts)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize sampler for hour {hour}.");
    }

    private static string PathFor(uint hour, float distanceScaler) =>
        Path.Combine(_cacheDir, $"sampler_{hour}_{distanceScaler}.json");
}
