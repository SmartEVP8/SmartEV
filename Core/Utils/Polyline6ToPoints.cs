namespace Core.Utils;

using Core.Shared;

/// <summary>
/// Implemented according to https://developers.google.com/maps/documentation/utilities/polylinealgorithm.
/// </summary>
public static class Polyline6ToPoints
{
    private const double _scale = 1e-5;

    /// <summary>
    /// Using 5 bits of the encoded chunk to represent the value, the remaining bit is used as a flag to indicate if there are more chunks to decode for the current value.
    /// </summary>
    private const int _chunkMask = 0b11111; // 0x1F in hexadecimal, 31 in decimal

    /// <summary>
    /// If the 6th bit of the encoded chunk is set, it indicates that there are more chunks to decode for the current value.
    /// </summary>
    private const int _moreDataFlag = 0b100000; // 0x20 in hexadecimal, 32 in decimal

    /// <summary>
    /// Decodes a polyline string into a list of (latitude, longitude) points.
    /// </summary>
    /// <param name="polyline">The string representing the encoded polyline, where each character encodes a portion of the latitude and longitude values. </param>
    /// <returns>Path containing a list of latitude and longitude points decoded from the input polyline string.</returns>
    public static Path DecodePolyline(string polyline)
    {
        var points = new List<Position>();

        var index = 0;

        // First decoded lat and longitude gives starting points.
        // Subsequent decoded lat and longitude values are offsets from the previous points.
        var lat = 0;
        var lng = 0;

        while (index < polyline.Length)
        {
            lat += DecodeValue(polyline, ref index);
            lng += DecodeValue(polyline, ref index);

            // Step 2
            points.Add(new Position(
                lat * _scale,
                lng * _scale));
        }

        return new Path(points);
    }

    /// <summary>
    /// Decodes a single value (lat or long) from the polyline string, starting at the specified index.
    /// The method reads the encoded chunks until it encounters a chunk that does not have the "more data" flag set.
    /// </summary>
    private static int DecodeValue(string polyline, ref int index)
    {
        var result = 0;
        var shift = 0;
        int encodedChunk;

        do
        {
            // Step 11 - 9
            encodedChunk = polyline[index++] - 63;

            // Step 8 - 6
            var decodedChunk = encodedChunk & _chunkMask;
            var offsetChunk = decodedChunk << shift;
            result |= offsetChunk;
            shift += 5;
        }
        while (encodedChunk >= _moreDataFlag);

        // Step 5
        var isNegative = (result & 1) != 0;

        // Step 4 - 3
        return isNegative
            ? ~(result >> 1)
            : (result >> 1);
    }
}
