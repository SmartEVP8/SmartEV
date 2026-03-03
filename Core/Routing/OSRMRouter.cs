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
        float* outBuffer);

    [LibraryImport(_lib)]
    private static partial IntPtr ComputeSrcToDest(
        IntPtr osrm,
        double evLon,
        double evLat,
        double destLon,
        double destLat);

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
            coords[i * 2] = stations[i].Position.Latitude;
            coords[(i * 2) + 1] = stations[i].Position.Longitude;
        }

        RegisterStations(_osrm, coords, stations.Count);
    }

    public float[] QueryStations(double evLon, double evLat, int[] indices)
    {
        if (indices.Length == 0)
            return [];

        var result = new float[indices.Length];

        unsafe
        {
            fixed (float* ptr = result)
            {
                ComputeTableIndexed(_osrm, evLon, evLat, indices, indices.Length, ptr);
            }
        }

        return result;
    }

    public (float duration, string polyline) QuerySingleDestination(double evLon, double evLat, double destLon, double destLat)
    {
        var resultPtr = ComputeSrcToDest(_osrm, evLon, evLat, destLon, destLat);

        if (resultPtr == IntPtr.Zero)
        {
            Console.WriteLine("DEBUG: Route returned null");
            return (-1, string.Empty);
        }

        var result = Marshal.PtrToStructure<RouteResult>(resultPtr);
        var polylineStr = Marshal.PtrToStringAnsi(result.Polyline);

        FreeMemory(result.Polyline);
        FreeMemory(resultPtr);

        return (duration: result.Duration, polyline: polylineStr);
    }

    public void Dispose() => DeleteOSRM(_osrm);
}
