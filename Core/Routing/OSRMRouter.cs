using System.Runtime.InteropServices;
using Core.Charging;

public unsafe partial class OSRMRouter : IDisposable
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
        int numStations);

    [LibraryImport(_lib)]
    private static partial void ComputeTableIndexed(
        IntPtr osrm,
        double evLon,
        double evLat,
        [In] int[] indices,
        int numIndices,
        float* outDurations,
        float* outDistances);

    [LibraryImport(_lib)]
    private static partial IntPtr ComputeSrcToDest(
        IntPtr osrm,
        double evLon,
        double evLat,
        double destLon,
        double destLat);

    [LibraryImport(_lib)]
    private static partial void PointsToPoints(
        IntPtr osrm,
        [In] double[] srcCoords,
        int numSrcs,
        [In] double[] dstCoords,
        int numDsts,
        float* outDurations,
        float* outDistances);

    [StructLayout(LayoutKind.Sequential)]
    private struct RouteResult
    {
        public float Duration;
        public IntPtr Polyline;
    }

    public OSRMRouter(string mapPath)
    {
        _osrm = InitializeOSRM(mapPath);
        if (_osrm == IntPtr.Zero)
            throw new Exception("OSRM init failed.");
    }

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

    public (float[] durations, float[] distances) QueryStations(double evLon, double evLat, int[] indices)
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

    public (float duration, string polyline) QuerySingleDestination(double evLon, double evLat, double destLon, double destLat)
    {
        var resultPtr = ComputeSrcToDest(_osrm, evLon, evLat, destLon, destLat);
        if (resultPtr == IntPtr.Zero)
            return (-1, string.Empty);

        var result = Marshal.PtrToStructure<RouteResult>(resultPtr);
        var polylineStr = Marshal.PtrToStringAnsi(result.Polyline);

        FreeMemory(result.Polyline);
        FreeMemory(resultPtr);

        return (result.Duration, polylineStr);
    }

    public (float[] durations, float[] distances) QueryPointsToPoints(
        double[] srcCoords, int numSrcs,
        double[] dstCoords, int numDsts)
    {
        var durations = new float[numSrcs * numDsts];
        var distances = new float[numSrcs * numDsts];

        fixed (float* durPtr = durations)
        fixed (float* distPtr = distances)
        {
            PointsToPoints(_osrm, srcCoords, numSrcs, dstCoords, numDsts, durPtr, distPtr);
        }

        return (durations, distances);
    }

    public void Dispose() => DeleteOSRM(_osrm);
}