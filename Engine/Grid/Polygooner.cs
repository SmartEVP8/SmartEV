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

        var gridCells = new GridCell[latSteps][];

        for (var i = 0; i < latSteps; i++)
        {
            for (var j = 0; j < lonSteps; j++)
            {
                var centerLat = min.Latitude + ((i + 0.5) * size);
                var centerLon = min.Longitude + ((j + 0.5) * lonSize);
                var centerPos = new Position(centerLat, centerLon);

                gridCells[i][j] = new GridCell(false, centerPos);

                foreach (var polygon in polygons)
                {
                    if (PointInPolygon(polygon, centerLat, centerLon))
                    {
                        gridCells[i][j].Spawnable = true;
                        break;
                    }
                }
            }
        }

        return new Grid(gridCells);
    }

    /// <summary>
    /// Ray casting algorithm for point-in-polygon testing.
    /// https://www.geeksforgeeks.org/c/point-in-polygon-in-c/.
    /// </summary>
    private static bool PointInPolygon(List<Position> polygon, double lat, double lon)
    {
        var inside = false;
        var vertexCount = polygon.Count;

        for (int current = 0, previous = vertexCount - 1; current < vertexCount; previous = current++)
        {
            var currentLon = polygon[current].Longitude;
            var currentLat = polygon[current].Latitude;
            var previousLon = polygon[previous].Longitude;
            var previousLat = polygon[previous].Latitude;

            var edgeStradlesPoint = (currentLat > lat) != (previousLat > lat);
            var rayIntersectsEdge = lon < ((previousLon - currentLon) * (lat - currentLat) / (previousLat - currentLat)) + currentLon;

            if (edgeStradlesPoint && rayIntersectsEdge)
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

        return (new Position(minLat, minLon), new Position(maxLat, maxLon));
    }
}
