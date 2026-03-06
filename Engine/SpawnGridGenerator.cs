using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.IO;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Engine
{
    public static class SpawnGridGenerator
    {
        public const int CellSizeMeters = 10_000;
        public const int TargetEpsg = 25832; // EPSG:25832 (ETRS89 / UTM zone 32N)

        /// <summary>
        /// Generates a spawn grid JSON using a Denmark boundary polygon GeoJSON (FeatureCollection).
        /// Cells are spawnable (1) if they contain ANY land (even partial land).
        /// Cells that are purely water are 0.
        ///
        /// Input polygon is assumed EPSG:4326 (lon/lat).
        /// Output grid is computed in EPSG:25832 metres.
        /// </summary>
        public static void Generate(
            string denmarkBoundaryGeoJsonPath,
            string outputSpawnGridJsonPath,
            int cellSizeMeters = CellSizeMeters)
        {
            if (cellSizeMeters <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellSizeMeters), "Cell size must be > 0.");

            // 1) Load ALL Denmark polygon geometries from GeoJSON (FeatureCollection) and union them
            var wgs84Geom = LoadUnionGeometryFromFeatureCollection(denmarkBoundaryGeoJsonPath);

            // 2) Reproject to EPSG:25832 (metres)
            var land25832 = ReprojectWgs84ToEpsg25832(wgs84Geom);
            land25832.SRID = TargetEpsg;

            // Optional: fixes minor topology issues in some boundary datasets
            // land25832 = land25832.Buffer(0);

            // 3) Build grid (1 = any land in cell; 0 = pure water)
            var (grid, width, height, minx, miny) = BuildGridAnyLand(land25832, cellSizeMeters);

            // 4) Write output JSON
            var payload = new SpawnGridPayload
            {
                crs = $"EPSG:{TargetEpsg}",
                cellSizeMeters = cellSizeMeters,
                regions = new[] { "Denmark (admin boundary, GeoJSON union)" },
                originMeters = new OriginMeters { x = minx, y = miny },
                width = width,
                height = height,
                grid = grid
            };

            WriteJson(outputSpawnGridJsonPath, payload);
        }

        // ----------------------------
        // GeoJSON loading (robust: unions all polygon features)
        // ----------------------------

        private static Geometry LoadUnionGeometryFromFeatureCollection(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"GeoJSON not found: {path}");

            var json = File.ReadAllText(path);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElem) || typeElem.GetString() != "FeatureCollection")
                throw new InvalidOperationException("Expected a GeoJSON FeatureCollection as input.");

            var features = root.GetProperty("features");
            if (features.ValueKind != JsonValueKind.Array || features.GetArrayLength() == 0)
                throw new InvalidOperationException("GeoJSON FeatureCollection has no features.");

            var reader = new GeoJsonReader();

            Geometry? acc = null;

            foreach (var feat in features.EnumerateArray())
            {
                if (!feat.TryGetProperty("geometry", out var geomElem) || geomElem.ValueKind == JsonValueKind.Null)
                    continue;

                var geom = reader.Read<Geometry>(geomElem.GetRawText());

                if (geom is not Polygon && geom is not MultiPolygon)
                    continue;

                geom.SRID = 4326; // lon/lat

                acc = acc == null ? geom : acc.Union(geom);
            }

            if (acc == null)
                throw new InvalidOperationException("No Polygon/MultiPolygon geometries found in FeatureCollection.");

            return acc;
        }

        // ----------------------------
        // Reprojection
        // ----------------------------

        private static Geometry ReprojectWgs84ToEpsg25832(Geometry geom4326)
        {
            if (geom4326 == null) throw new ArgumentNullException(nameof(geom4326));

            // WGS84 geographic
            var wgs84 = GeographicCoordinateSystem.WGS84;

            // EPSG:25832 is ETRS89 / UTM zone 32N.
            // ProjNet doesn't ship a full EPSG registry; define UTM 32N on GRS80.
            var etrs89 = GeographicCoordinateSystem.GRS80;
            var utm32N = ProjectedCoordinateSystem.UTM(etrs89, 32, true);

            var ctf = new CoordinateTransformationFactory();
            var transform = ctf.CreateFromCoordinateSystems(wgs84, utm32N).MathTransform;

            var gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: TargetEpsg);

            return NetTopologySuite.CoordinateSystems.Transformations.GeometryTransform.TransformGeometry(gf, geom4326, transform);
        }

        // ----------------------------
        // Grid building: "any land in cell => 1"
        // ----------------------------

        private static (int[][] grid, int width, int height, double minx, double miny)
            BuildGridAnyLand(Geometry land, int cellSizeMeters)
        {
            // Prepared geometry makes repeated spatial predicates much faster
            var prepared = PreparedGeometryFactory.Prepare(land);

            var env = land.EnvelopeInternal;

            // Snap origin to the cell grid so the output is stable across runs
            var minx = Math.Floor(env.MinX / cellSizeMeters) * cellSizeMeters;
            var miny = Math.Floor(env.MinY / cellSizeMeters) * cellSizeMeters;
            var maxx = Math.Ceiling(env.MaxX / cellSizeMeters) * cellSizeMeters;
            var maxy = Math.Ceiling(env.MaxY / cellSizeMeters) * cellSizeMeters;

            var width = (int)((maxx - minx) / cellSizeMeters);
            var height = (int)((maxy - miny) / cellSizeMeters);

            var gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: land.SRID);

            var grid = new int[height][];

            // Small speed win: reuse the Envelope object by mutating values (NTS Envelope is mutable)
            for (int j = 0; j < height; j++)
            {
                grid[j] = new int[width];

                var y0 = miny + j * cellSizeMeters;
                var y1 = y0 + cellSizeMeters;

                for (int i = 0; i < width; i++)
                {
                    var x0 = minx + i * cellSizeMeters;
                    var x1 = x0 + cellSizeMeters;

                    // Cell polygon
                    var cellEnv = new Envelope(x0, x1, y0, y1);

                    // Fast reject: if bbox doesn't intersect Denmark bbox, it can't intersect geometry
                    if (!cellEnv.Intersects(env))
                    {
                        grid[j][i] = 0;
                        continue;
                    }

                    var cellGeom = gf.ToGeometry(cellEnv);

                    // Core rule:
                    // 1 if the Denmark land geometry touches any part of this cell (partially land or fully land)
                    // 0 if there's no intersection (pure water cell)
                    grid[j][i] = prepared.Intersects(cellGeom) ? 1 : 0;
                }
            }

            return (grid, width, height, minx, miny);
        }

        // ----------------------------
        // JSON output
        // ----------------------------

        private static void WriteJson(string path, SpawnGridPayload payload)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var options = new JsonSerializerOptions { WriteIndented = true };

            File.WriteAllText(path, JsonSerializer.Serialize(payload, options));
        }

        // ----------------------------
        // Output model
        // ----------------------------

        private sealed class SpawnGridPayload
        {
            public string crs { get; set; } = "";
            public int cellSizeMeters { get; set; }
            public string[] regions { get; set; } = Array.Empty<string>();
            public OriginMeters originMeters { get; set; } = new();
            public int width { get; set; }
            public int height { get; set; }
            public int[][] grid { get; set; } = Array.Empty<int[]>();
        }

        private sealed class OriginMeters
        {
            public double x { get; set; }
            public double y { get; set; }
        }
    }
}