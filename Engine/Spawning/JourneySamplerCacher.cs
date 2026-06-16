namespace Engine.Spawning;

using System.IO;
using Core.Shared;

/// <summary>
/// Utility class for caching journey samplers to a fast binary format.
/// </summary>
internal static class JourneySamplerCache
{
    private const string _cacheDir = "journeySamples";
    private const string _formatSchemaVersion = "v2";

    public static void EnsureDirectory() => Directory.CreateDirectory(_cacheDir);

    public static bool Exists(uint hour, float distanceScalar)
    {
        var path = PathFor(hour, distanceScalar);
        var info = new FileInfo(path);
        return info.Exists && info.Length > 32;
    }

    public static void Write(uint hour, JourneySamplerDTO dto, float distanceScalar)
    {
        using var stream = new FileStream(PathFor(hour, distanceScalar), FileMode.Create, FileAccess.Write, FileShare.None, 4096);
        using var writer = new BinaryWriter(stream);

        writer.Write(dto.HalfLat);
        writer.Write(dto.HalfLon);

        WriteFloatArray(writer, dto.SourceWeights);

        writer.Write(dto.DestinationWeights.Length);
        foreach (var row in dto.DestinationWeights)
        {
            WriteFloatArray(writer, row);
        }

        WritePositions(writer, dto.CellCenters);
        WritePositions(writer, dto.CityCenters);

        writer.Write(dto.WetPolygons.Count);
        foreach (var poly in dto.WetPolygons)
        {
            WritePositions(writer, poly);
        }
    }

    public static JourneySamplerDTO Read(uint hour, float distanceScalar)
    {
        using var stream = new FileStream(PathFor(hour, distanceScalar), FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
        using var reader = new BinaryReader(stream);

        var halfLat = reader.ReadDouble();
        var halfLon = reader.ReadDouble();

        var sourceWeights = ReadFloatArray(reader);

        var destLen = reader.ReadInt32();
        var destinationWeights = new float[destLen][];
        for (var i = 0; i < destLen; i++)
        {
            destinationWeights[i] = ReadFloatArray(reader);
        }

        var cellCenters = ReadPositionsArray(reader);
        var cityCenters = ReadPositionsArray(reader);

        var polyCount = reader.ReadInt32();
        var wetPolygons = new List<List<Position>>(polyCount);
        for (var i = 0; i < polyCount; i++)
        {
            wetPolygons.Add(ReadPositionsList(reader));
        }

        return new JourneySamplerDTO(
            sourceWeights,
            destinationWeights,
            cellCenters,
            cityCenters,
            halfLat,
            halfLon,
            wetPolygons);
    }

    private static void WriteFloatArray(BinaryWriter writer, float[] array)
    {
        writer.Write(array.Length);
        foreach (var val in array) writer.Write(val);
    }

    private static float[] ReadFloatArray(BinaryReader reader)
    {
        var len = reader.ReadInt32();
        var array = new float[len];
        for (var i = 0; i < len; i++) array[i] = reader.ReadSingle();
        return array;
    }

    private static void WritePositions(BinaryWriter writer, IReadOnlyCollection<Position> positions)
    {
        writer.Write(positions.Count);
        foreach (var pos in positions)
        {
            writer.Write(pos.Longitude);
            writer.Write(pos.Latitude);
        }
    }

    private static Position[] ReadPositionsArray(BinaryReader reader)
    {
        var len = reader.ReadInt32();
        var array = new Position[len];
        for (var i = 0; i < len; i++)
        {
            array[i] = new Position(reader.ReadDouble(), reader.ReadDouble());
        }
        return array;
    }

    private static List<Position> ReadPositionsList(BinaryReader reader)
    {
        var len = reader.ReadInt32();
        var list = new List<Position>(len);
        for (var i = 0; i < len; i++)
        {
            list.Add(new Position(reader.ReadDouble(), reader.ReadDouble()));
        }
        return list;
    }

    private static string PathFor(uint hour, float distanceScalar) =>
        Path.Combine(_cacheDir, $"sampler_{_formatSchemaVersion}_{hour}_{distanceScalar}.bin");
}
