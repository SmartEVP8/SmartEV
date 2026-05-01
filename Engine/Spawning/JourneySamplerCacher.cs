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
    /// <returns>Returns true if the file exist.</returns>
    public static bool Exists(uint hour)
    {
        var path = PathFor(hour);
        return File.Exists(path) && new FileInfo(path).Length > 10;
    }

    /// <summary>
    /// Writes the given sampler DTO to disk for the specified hour.
    /// </summary>
    /// <param name="hour">The hour for which time the sampler was computed for.</param>
    /// <param name="dto">The sampler the should be written to file.</param>
    public static void Write(uint hour, JourneySamplerDto dto)
    {
        var json = JsonSerializer.Serialize(dto, _jsonOpts);

        if (json.Length < 10)
        {
            throw new InvalidOperationException(
                $"Serialised DTO for hour {hour} is suspiciously empty: {json}");
        }

        File.WriteAllText(PathFor(hour), json);
    }

    /// <summary>
    /// Reads the sampler from the specified hour.
    /// </summary>
    /// <param name="hour">The hour for which sampler is needed.</param>
    /// <returns>Returns a JourneySamplerDTO.</returns>
    public static JourneySamplerDto Read(uint hour)
    {
        var json = File.ReadAllText(PathFor(hour));
        return JsonSerializer.Deserialize<JourneySamplerDto>(json, _jsonOpts)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize sampler for hour {hour}.");
    }

    private static string PathFor(uint hour) =>
        Path.Combine(_cacheDir, $"sampler_{hour}.json");
}
