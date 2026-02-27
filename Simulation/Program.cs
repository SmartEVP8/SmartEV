using System.Diagnostics;
using System.Text.Json;
using Core.Classes;

public static class Program
{
  public static void Main()
  {
    var jsonPath = "denmark_ev_data_projected.json";
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    var evData = JsonSerializer.Deserialize<List<EvStationData>>(
        File.ReadAllText(jsonPath),
        options) ?? [];

    var targetStationCount = evData.Count; // Use all stations from the JSON file

    var stations = evData
        .Take(targetStationCount)
        .Select((data, index) => new Station
        {
          Id = index,
          Lon = data.AddressInfo.Longitude,
          Lat = data.AddressInfo.Latitude,
        })
        .ToList();

    var indices = Enumerable.Range(0, stations.Count).ToArray();

    using var router = new OSRMRouter(
        "../Core/data/output.osrm");

    router.InitStations(stations);

    var evCoordinates = new (double Lon, double Lat)[]
    {
            (9.9410, 57.2706),  // Brønderslev
            (9.9217, 57.0488),  // Aalborg
            (10.0364, 56.4606), // Randers
            (10.2039, 56.1629), // Aarhus
            (12.5683, 55.6761), // København
    };

    var duration = router.QuerySingleDestination(9.9410, 57.2706, 9.9217, 57.0488);
    Console.WriteLine($"Duration from Brønderslev to Aalborg: {duration} seconds");

    var minusOneStations = new List<(int EvIndex, Station Station)>();
    uint numberOfMinus1 = 0;

    for (var i = 0; i < evCoordinates.Length; i++)
    {
      var (lon, lat) = evCoordinates[i];

      var durations = router.QueryStations(lon, lat, indices);

      Console.WriteLine($"Query {i + 1} ({lon}, {lat}):");

      for (var j = 0; j < durations.Length; j++)
      {
        if (durations[j] < 0)
        {
          minusOneStations.Add((i, stations[j]));
          numberOfMinus1++;
        }
      }
    }

    Console.WriteLine($"Total number of -1 durations: {numberOfMinus1}");
    Console.WriteLine("EV coordinate → stations with -1 durations:");

    foreach (var group in minusOneStations.GroupBy(e => e.EvIndex))
    {
      var ev = evCoordinates[group.Key];
      Console.WriteLine($"EV {group.Key} ({ev.Lat}, {ev.Lon}):");
      foreach (var entry in group)
      {
        var s = entry.Station;
        Console.WriteLine($"  Station {s.Id}: ({s.Lat}, {s.Lon})");
      }
    }
    var sw = Stopwatch.StartNew();
    var chargingModel = new ChargingModel(0.0001);
    sw.Stop();

    Console.WriteLine($"Build time: {sw.Elapsed.TotalMilliseconds:F3} ms");

    //---------------------------------
    // USER EXAMPLE
    //---------------------------------
    double socStart = 0.05;
    double socEnd = 1;
    double capacity = 83.9;
    double charger = 205;

    double hours = chargingModel.GetChargingTimeHours(
        socStart,
        socEnd,
        capacity,
        charger);

    Console.WriteLine($"Charging time: {hours * 60:F1} minutes");

    //---------------------------------
    // SINGLE CALL BENCHMARK
    //---------------------------------
    sw.Restart();

    for (int i = 0; i < 1_000_000; i++)
    {
      chargingModel.GetChargingTimeHours(
          socStart, socEnd, capacity, charger);
    }

    sw.Stop();

    Console.WriteLine(
        $"1M calls: {sw.Elapsed.TotalMilliseconds:F2} ms");

    //---------------------------------
    // LARGE PERFORMANCE TEST
    //---------------------------------
    const int N = 10_000_000;

    sw.Restart();

    double sum = 0; // prevents optimizer removing calls

    for (int i = 0; i < N; i++)
    {
      sum += chargingModel.GetChargingTimeHours(
          socStart, socEnd, capacity, charger);
    }

    sw.Stop();

    double nsPerCall =
        sw.Elapsed.TotalMilliseconds * 1_000_000 / N;

    Console.WriteLine($"10M calls: {sw.Elapsed.TotalSeconds:F3} s");
    Console.WriteLine($"Avg time per call: {nsPerCall:F1} ns");

    Console.WriteLine(sum); // anti-optimization
  }
}
