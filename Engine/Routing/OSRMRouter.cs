namespace Engine.Routing;

using System.Runtime.InteropServices;
using Core.Charging;

/// <summary>
/// Provides routing and station query functionality using the OSRM (Open Source Routing Machine) wrapper library.
/// </summary>
public unsafe partial class OSRMRouter : IDisposable, IMatrixRouter, IDestinationRouter
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
    private static partial void ComputeTableIndexed(
        IntPtr osrm,
        double evLon,
        double evLat,
        [In] int[] indices,
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
    private static partial IntPtr ComputeSrcToDestWithStops(
        IntPtr osrm,
        [In] double[] coords,
        int numCoords
    );

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
        public IntPtr Polyline;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OSRMRouter"/> class.
    /// </summary>
    /// <param name="mapPath">The path to the OSRM map data file.</param>
    /// <exception cref="InvalidOperationException">Thrown when OSRM initialization fails.</exception>
    public OSRMRouter(string mapPath)
    {
        _osrm = InitializeOSRM(mapPath);
        if (_osrm == IntPtr.Zero)
            throw new Exception("OSRM initialization failed.");
    }

    /// <summary>
    /// Initializes the router with a list of charging stations.
    /// </summary>
    /// <param name="stations">The list of charging stations to register.</param>
    public void InitStations(List<Station> stations)
    {
        var coords = new double[stations.Count * 2];

        for (var i = 0; i < stations.Count; i++)
        {
            coords[i * 2] = stations[i].Position.Longitude;
            coords[(i * 2) + 1] = stations[i].Position.Latitude;
        }

        RegisterStations(_osrm, coords, stations.Count);
    }

    /// <summary>
    /// Queries the durations and distances from an electric vehicle to specified stations.
    /// </summary>
    /// <param name="evLon">The longitude coordinate of the electric vehicle.</param>
    /// <param name="evLat">The latitude coordinate of the electric vehicle.</param>
    /// <param name="indices">An array of station indices to query.</param>
    /// <returns>A tuple containing arrays of durations and distances to each station.</returns>
    public (float[] durations, float[] distances) QueryStations(
        double evLon,
        double evLat,
        int[] indices)
    {
        if (indices.Length == 0)
            return ([], []);

        var durations = new float[indices.Length];
        var distances = new float[indices.Length];

        fixed (float* durPtr = durations)
        fixed (float* distPtr = distances)
        {
            ComputeTableIndexed(_osrm, evLon, evLat, indices, indices.Length, durPtr, distPtr);
        }

        return (durations, distances);
    }

    /// <summary>
    /// Queries the duration and polyline route from an electric vehicle to a single destination.
    /// </summary>
    /// <param name="coords">An array of coordinates representing the route, where the first element is the source and the last element is the destination. Intermediate elements represent stops.</param>
    /// <returns>A tuple containing the duration and polyline string for the route.</returns>
    public (float duration, string polyline) QueryDestination(double[] coords)
    {
        nint resultPtr;
        if (coords.Length < 4)
        {
            throw new ArgumentException("At least two coordinates are required for a source and destination.");
        }
        else if (coords.Length % 2 != 0)
        {
            throw new ArgumentException("Coordinates array must contain pairs of longitude and latitude.");
        }

        if (coords.Length == 4)
        {
            resultPtr = ComputeSrcToDest(
                _osrm,
                coords[0],
                coords[1],
                coords[2],
                coords[3]);
        }
        else if (coords.Length > 4)
        {
            resultPtr = ComputeSrcToDestWithStops(_osrm, coords, coords.Length);
        }
        else
        {
            resultPtr = IntPtr.Zero;
        }

        if (resultPtr == IntPtr.Zero)
            return (-1, string.Empty);

        var result = Marshal.PtrToStructure<RouteResult>(resultPtr);
        var polylineStr = Marshal.PtrToStringAnsi(result.Polyline)!;

        FreeMemory(result.Polyline);
        FreeMemory(resultPtr);

        return (result.Duration, polylineStr);
    }

    /// <summary>
    /// Queries durations and distances between multiple source and destination points.
    /// </summary>
    /// <param name="srcCoords">Array of source coordinates in [lon, lat, lon, lat, ...] format.</param>
    /// <param name="dstCoords">Array of destination coordinates in [lon, lat, lon, lat, ...] format.</param>
    /// <returns>A tuple containing matrices of durations and distances between all source and destination pairs.</returns>
    public (float[] durations, float[] distances) QueryPointsToPoints(
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

        return (durations, distances);
    }

    /// <summary>
    /// Disposes the OSRM router and releases unmanaged resources.
    /// </summary>
    public void Dispose() => DeleteOSRM(_osrm);
}
