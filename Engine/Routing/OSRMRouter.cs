namespace Engine.Routing;

using System.Runtime.InteropServices;
using Core.Charging;
using Core.Shared;

public record RoutingResult(float[] Durations, float[] Distances);

public record RoutingResultLegs((float Duration, float Distance)[] SrcToStation, (float Duration, float Distance)[] StationToDest)
{
    /// <summary>
    /// Convert legs into a totals <see cref="RoutingResult"/> with per-station total duration and distance.
    /// </summary>
    /// <returns>A <see cref="RoutingResult"/> with the total durations and distances for each station.</returns>
    public RoutingResult ToTotals()
    {
        if (SrcToStation is null || StationToDest is null)
            return new RoutingResult([], []);

        var n = SrcToStation.Length;
        var durations = new float[n];
        var distances = new float[n];
        for (var i = 0; i < n; i++)
        {
            durations[i] = SrcToStation[i].Duration + StationToDest[i].Duration;
            distances[i] = SrcToStation[i].Distance + StationToDest[i].Distance;
        }

        return new RoutingResult(durations, distances);
    }
}

public record RouteSegment(float Duration, float Distance, string Polyline);

/// <summary>
/// Provides routing and station query functionality using the OSRM (Open Source Routing Machine) wrapper library.
/// </summary>
public unsafe partial class OSRMRouter : IDisposable, IOSRMRouter
{
    private const string _lib = "osrm_wrapper";
    private readonly IntPtr _osrm;

    [LibraryImport(_lib, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr InitializeOSRM(string path);

    [LibraryImport(_lib)]
    private static partial void DeleteOSRM(IntPtr osrm);

    [LibraryImport(_lib)]
    private static partial void FreeMemory(IntPtr ptr);

    [LibraryImport(_lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterStations(
        IntPtr osrm,
        [In] double[] coords,
        int numStations,
        [Out] double[] outSnappedCoords
    );

    [LibraryImport(_lib)]
    private static partial void ComputeTableIndexedWithDest(
            IntPtr osrm,
            double evLon,
            double evLat,
            double destLon,
            double destLat,
            [In] ushort[] indices,
            int numIndices,
            [Out] TableResult[] outResults
        );

    [LibraryImport(_lib)]
    private static partial IntPtr ComputeSrcToDest(
        IntPtr osrm,
        double evLon,
        double evLat,
        double destLon,
        double destLat);

    [LibraryImport(_lib)]
    private static partial IntPtr ComputeSrcToDestWithStop(
        IntPtr osrm,
        double evLon,
        double evLat,
        double stationLon,
        double stationLat,
        double destLon,
        double destLat,
        ushort index);

    [LibraryImport(_lib)]
    private static partial void PointsToPoints(
        IntPtr osrm,
        [In] double[] srcCoords,
        int numSrcs,
        [In] double[] dstCoords,
        int numDsts,
        float* outDurations,
        float* outDistances
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct RouteResult
    {
        public float Duration;
        public float Distance;
        public IntPtr Polyline;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TableLeg
    {
        public float Durations;
        public float Distances;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TableResult
    {
        public TableLeg SrcToStation;
        public TableLeg StationToDest;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OSRMRouter"/> class.
    /// </summary>
    /// <param name="mapPath">The path to the OSRM map data file.</param>
    /// <param name="stations">A list of charging stations to register with the router.</param>
    /// <exception cref="InvalidOperationException">Thrown when OSRM initialization fails.</exception>
    public OSRMRouter(FileInfo mapPath, List<Station> stations)
    {
        _osrm = InitializeOSRM(mapPath.ToString());
        if (_osrm == IntPtr.Zero)
            throw new Exception("OSRM initialization failed.");
        InitStations(stations);
    }

    /// <inheritdoc/>
    public RoutingResultLegs QueryStationsWithDest(
        double evLon,
        double evLat,
        double destLon,
        double destLat,
        ushort[] indices)
    {
        if (indices.Length == 0)
            return new RoutingResultLegs([], []);

        var outResult = new TableResult[indices.Length];
        ComputeTableIndexedWithDest(_osrm, evLon, evLat, destLon, destLat, indices, indices.Length, outResult);

        var srcToStation = new (float Duration, float Distance)[indices.Length];
        var stationToDest = new (float Duration, float Distance)[indices.Length];

        for (var i = 0; i < indices.Length; i++)
        {
            srcToStation[i] = (
                outResult[i].SrcToStation.Durations * Time.MillisecondsPerSecond,
                outResult[i].SrcToStation.Distances
            );

            stationToDest[i] = (
                outResult[i].StationToDest.Durations * Time.MillisecondsPerSecond,
                outResult[i].StationToDest.Distances
            );
        }

        return new RoutingResultLegs(srcToStation, stationToDest);
    }

    /// <inheritdoc/>
    public RouteSegment QuerySingleDestination(
        double evLon,
        double evLat,
        double destLon,
        double destLat)
    {
        IntPtr resultPtr;

        resultPtr = ComputeSrcToDest(
            _osrm,
            evLon,
            evLat,
            destLon,
            destLat);

        if (resultPtr == IntPtr.Zero)
            return new RouteSegment(-1, -1, string.Empty);

        var result = Marshal.PtrToStructure<RouteResult>(resultPtr);
        var polylineStr = Marshal.PtrToStringAnsi(result.Polyline)!;

        FreeMemory(result.Polyline);
        FreeMemory(resultPtr);

        return new RouteSegment(result.Duration * Time.MillisecondsPerSecond, result.Distance, polylineStr);
    }

    /// <inheritdoc/>
    public RouteSegment QueryDestinationWithStop(double evLon, double evLat, double stationLon, double stationLat, double destLon, double destLat, ushort index = ushort.MaxValue)
    {
        IntPtr resultPtr;

        resultPtr = ComputeSrcToDestWithStop(_osrm, evLon, evLat, stationLon, stationLat, destLon, destLat, index);

        if (resultPtr == IntPtr.Zero)
            return new RouteSegment(-1, -1, string.Empty);

        var result = Marshal.PtrToStructure<RouteResult>(resultPtr);
        var polylineStr = Marshal.PtrToStringAnsi(result.Polyline)!;

        FreeMemory(result.Polyline);
        FreeMemory(resultPtr);

        return new RouteSegment(result.Duration * Time.MillisecondsPerSecond, result.Distance, polylineStr);
    }

    /// <inheritdoc/>
    public RoutingResult QueryPointsToPoints(
        double[] srcCoords,
        double[] dstCoords)
    {
        var numSrcs = srcCoords.Length / 2;
        var numDsts = dstCoords.Length / 2;
        var durations = new float[numSrcs * numDsts];
        var distances = new float[numSrcs * numDsts];

        fixed (float* durPtr = durations)
        fixed (float* distPtr = distances)
        {
            PointsToPoints(_osrm, srcCoords, numSrcs, dstCoords, numDsts, durPtr, distPtr);
        }

        for (var i = 0; i < durations.Length; i++)
            durations[i] *= Time.MillisecondsPerSecond;

        return new RoutingResult(durations, distances);
    }

    /// <summary>
    /// Disposes the OSRM router and releases unmanaged resources.
    /// </summary>
    public void Dispose() => DeleteOSRM(_osrm);

    /// <summary>
    /// Initializes the router with a list of charging stations.
    /// </summary>
    /// <param name="stations">The list of charging stations to register.</param>
    private void InitStations(List<Station> stations)
    {
        var coords = new double[stations.Count * 2];
        var snappedCoords = new double[stations.Count * 2];

        for (var i = 0; i < stations.Count; i++)
        {
            coords[i * 2] = stations[i].Position.Longitude;
            coords[(i * 2) + 1] = stations[i].Position.Latitude;
        }

        var ok = RegisterStations(_osrm, coords, stations.Count, snappedCoords);

        if (!ok)
            throw new InvalidOperationException("Failed to snap one or more stations to the road network.");

        for (var i = 0; i < stations.Count; i++)
        {
            var newPos = new Position(Longitude: snappedCoords[i * 2], Latitude: snappedCoords[(i * 2) + 1]);
            stations[i].SetPosition(newPos);
        }
    }
}