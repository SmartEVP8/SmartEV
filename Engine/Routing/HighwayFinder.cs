namespace Engine.Routing;

using System.Collections.Generic;
using System.Text.Json;
using Core.Shared;

/// <summary>
/// Provides functionality for finding and processing highway nodes.
/// </summary>
public static class HighwayFinder
{
    /// <summary>
    /// Reads the highway nodes from the specified JSON file and returns them as a list of lists of positions.
    /// </summary>
    /// <param name="highwayPolylines">The file containing the highway nodes in JSON format.</param>
    /// <returns>Returns a list of lists of positions.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the routing file is not found.</exception>
    /// <exception cref="InvalidDataException">Thrown when the routing file is empty or contains invalid data.</exception>
    public static List<List<Position>> GetHighwayNodes(FileInfo highwayPolylines)
    {
        if (!highwayPolylines.Exists)
        {
            throw new FileNotFoundException($"The routing file '{highwayPolylines.FullName}' could not be found.");
        }

        // Open a stream to read the file efficiently
        using var stream = highwayPolylines.OpenRead();

        // Deserialize directly from the stream, falling back to an empty list if the file is empty/null
        return JsonSerializer.Deserialize<List<List<Position>>>(stream)
               ?? throw new InvalidDataException($"The routing file '{highwayPolylines.FullName}' is empty or contains invalid data.");
    }
}
