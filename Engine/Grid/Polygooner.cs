namespace Engine.Grid;

using Core.Shared;

public static class Polygooner
{
    /// <summary>
    /// Generates a grid of the specified size (in degrees) and marks cells that are inside any of the provided polygons.
    /// </summary>
    /// <param name="size">The size in degrees of each grid cell.</param>
    /// <param name="polygons">The polygons to check intersections against.</param>
    /// <returns>A 2D grid with 1 or 0.</returns>
    public static Grid GenerateGrid(double size, List<List<Position>> polygons)
    {
        var (min, max) = ComputeBoundingBox(polygons);
        var diffLat = max.Latitude - min.Latitude;
        var diffLon = max.Longitude - min.Longitude;

        // Scale longitude size to account for latitude compression in denmark
        var midLat = (min.Latitude + max.Latitude) / 2.0;
        var lonScale = Math.Cos(midLat * Math.PI / 180.0);
        var lonSize = size / lonScale;

        var latSteps = (int)Math.Ceiling(diffLat / size);
        var lonSteps = (int)Math.Ceiling(diffLon / lonSize);

        var gridCells = new List<List<GridCell>>(latSteps);

        for (var i = 0; i < latSteps; i++)
        {
            var row = new List<GridCell>(lonSteps);

            for (var j = 0; j < lonSteps; j++)
            {
                var centerLat = min.Latitude + ((i + 0.5) * size);
                var centerLon = min.Longitude + ((j + 0.5) * lonSize);
                var centerPos = new Position(centerLon, centerLat);
                var spawnable = polygons.Any(polygon => PointInPolygon(polygon, centerLon, centerLat));
                row.Add(new GridCell(spawnable, centerPos));
            }

            gridCells.Add(row);
        }

        return new Grid(gridCells, min, size, lonSize);
    }

    /// <summary>
    /// Determines if a point (lat, lon) is inside a polygon using the ray casting algorithm.
    ///
    /// The idea: cast an imaginary ray rightward from the test point and count how many
    /// polygon edges it crosses. Odd crossings = inside, even crossings = outside.
    ///
    /// This works because to get from inside a shape to outside, you must cross its boundary.
    /// </summary>
    private static bool PointInPolygon(List<Position> polygon, double lon, double lat)
    {
        var inside = false;
        var vertexCount = polygon.Count;

        for (var current = 0; current < vertexCount; current++)
        {
            var previous = (current + vertexCount - 1) % vertexCount;

            var currentLon = polygon[current].Longitude;
            var currentLat = polygon[current].Latitude;
            var previousLon = polygon[previous].Longitude;
            var previousLat = polygon[previous].Latitude;

            // Only edges where one vertex is above and one is below the test latitude can be crossed
            if ((currentLat > lat) == (previousLat > lat))
                continue;

            // Find the longitude where this edge crosses the test latitude
            var interpolationFactor = (lat - currentLat) / (previousLat - currentLat);
            var crossingLon = currentLon + ((previousLon - currentLon) * interpolationFactor);

            // If the crossing is to our right, our rightward ray hits it — toggle inside/outside
            if (lon < crossingLon)
                inside = !inside;
        }

        return inside;
    }

    private static (Position, Position) ComputeBoundingBox(List<List<Position>> polygons)
    {
        var minLat = double.MaxValue;
        var maxLat = double.MinValue;
        var minLon = double.MaxValue;
        var maxLon = double.MinValue;

        foreach (var polygon in polygons)
        {
            foreach (var point in polygon)
            {
                if (point.Latitude < minLat) minLat = point.Latitude;
                if (point.Latitude > maxLat) maxLat = point.Latitude;
                if (point.Longitude < minLon) minLon = point.Longitude;
                if (point.Longitude > maxLon) maxLon = point.Longitude;
            }
        }

        return (new Position(minLon, minLat), new Position(maxLon, maxLat));
    }
}
