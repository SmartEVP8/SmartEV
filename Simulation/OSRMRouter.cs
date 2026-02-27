using System.Runtime.InteropServices;
using Core.Classes;

public unsafe class OSRMRouter : IDisposable
{
  private const string _lib = "osrm_wrapper";

  [DllImport(_lib)]
  private static extern IntPtr InitializeOSRM(string path);

  [DllImport(_lib)]
  private static extern void RegisterStations(IntPtr osrm, double[] coords, int numStations);

  [DllImport(_lib)]
  private static extern float* ComputeTableIndexed(
      IntPtr osrm,
      double evLon,
      double evLat,
      int[] indices,
      int numIndices,
      out int outSize);

  [DllImport(_lib)]
  private static extern void FreeMemory(IntPtr ptr);

  [DllImport(_lib)]
  private static extern void DeleteOSRM(IntPtr osrm);

  [DllImport(_lib)]
  private static extern float* ComputeSrcToDest(
      IntPtr osrm,
      double evLon,
      double evLat,
      double destLon,
      double destLat,
      out int outSize);

  private readonly IntPtr _osrm;

  public OSRMRouter(string mapPath)
  {
    _osrm = InitializeOSRM(mapPath);
    if (_osrm == IntPtr.Zero)
      throw new Exception("OSRM init failed.");
  }

  public void InitStations(List<Station> stations)
  {
    double[] coords = new double[stations.Count * 2];

    for (int i = 0; i < stations.Count; i++)
    {
      stations[i].Id = i;
      coords[i * 2] = stations[i].Lon;
      coords[(i * 2) + 1] = stations[i].Lat;
    }

    RegisterStations(_osrm, coords, stations.Count);
  }

  public float[] QueryStations(double evLon, double evLat, int[] indices)
  {
    if (indices.Length == 0)
      return [];

    float* ptr = ComputeTableIndexed(_osrm, evLon, evLat, indices, indices.Length, out int size);

    if (ptr == null || size <= 0)
      return [];

    float[] result = new float[size];

    fixed (float* dest = result)
    {
      Buffer.MemoryCopy(ptr, dest, size * sizeof(float), size * sizeof(float));
    }

    FreeMemory((IntPtr)ptr);
    return result;
  }

  public float QuerySingleDestination(double evLon, double evLat, double destLon, double destLat)
  {
    float* ptr = ComputeSrcToDest(_osrm, evLon, evLat, destLon, destLat, out int size);
    if (ptr == null || size <= 0)
    {
      Console.WriteLine($"DEBUG: Route returned null or size={size}");
      return -1;
    }
    float result = *ptr;
    FreeMemory((IntPtr)ptr);
    return result;
  }

  public void Dispose() => DeleteOSRM(_osrm);
}
