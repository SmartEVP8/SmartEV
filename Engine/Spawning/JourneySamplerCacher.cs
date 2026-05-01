namespace Engine.Spawning;

using System.Text.Json;
internal static class JourneySamplerCache
{
    private const string CacheDir = "journeySamples";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
    };

    public static void EnsureDirectory() => Directory.CreateDirectory(CacheDir);

    public static bool Exists(uint hour)
    {
        var path = PathFor(hour);
        return File.Exists(path) && new FileInfo(path).Length > 10;
    }

    public static void Write(uint hour, JourneySamplerDto dto)
    {
        var json = JsonSerializer.Serialize(dto, JsonOpts);

        if (json.Length < 10)
        {
            throw new InvalidOperationException(
                $"Serialised DTO for hour {hour} is suspiciously empty: {json}");
        }

        File.WriteAllText(PathFor(hour), json);
    }

    public static JourneySamplerDto Read(uint hour)
    {
        var json = File.ReadAllText(PathFor(hour));
        return JsonSerializer.Deserialize<JourneySamplerDto>(json, JsonOpts)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize sampler for hour {hour}.");
    }

    private static string PathFor(uint hour) =>
        Path.Combine(CacheDir, $"sampler_{hour}.json");
}
