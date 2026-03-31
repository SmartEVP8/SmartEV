namespace Engine.Routing;

using System.Runtime.InteropServices;
using Core.Charging;

public record RoutingResult(float[] Durations, float[] Distances);

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
    private static partial void RegisterStations(
        IntPtr osrm,
        [In] double[] coords,
        int numStations
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
            float* outDurations,
            float* outDistances
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
    public RoutingResult QueryStationsWithDest(
        double evLon,
        double evLat,
        double destLon,
        double destLat,
        ushort[] indices)
    {
        if (indices.Length == 0)
            return new RoutingResult([], []);

        var durations = new float[indices.Length];
        var distances = new float[indices.Length];

        fixed (float* durPtr = durations)
        fixed (float* distPtr = distances)
        {
            ComputeTableIndexedWithDest(_osrm, evLon, evLat, destLon, destLat, indices, indices.Length, durPtr, distPtr);
        }

        return new RoutingResult(durations, distances);
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

        return new RouteSegment(result.Duration, result.Distance, polylineStr);
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

        return new RouteSegment(result.Duration, result.Distance, polylineStr);
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

        for (var i = 0; i < stations.Count; i++)
        {
            coords[i * 2] = stations[i].Position.Longitude;
            coords[(i * 2) + 1] = stations[i].Position.Latitude;
        }

        RegisterStations(_osrm, coords, stations.Count);
    }
}
