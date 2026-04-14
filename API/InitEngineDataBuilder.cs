namespace API;

using Core.Charging;
using Engine.Services;
using Protocol;

/// <summary>
/// Provides a method to build the initial engine data by querying the station service for the current state of all stations and chargers.
/// </summary>
public static class InitEngineDataBuilder
{
    /// <summary>
    /// Builds the initial engine data by querying the station service for the current state of all stations and chargers.
    /// </summary>
    /// <param name="stationService">The station service to query.</param>
    /// <returns>The initial engine data.</returns>
    public static InitEngineData BuildInitEngineData(StationService stationService)
    {
        var initData = new InitEngineData();

        try
        {
            foreach (var station in stationService.GetAllStations())
            {
                var stationInit = new StationInit
                {
                    Id = station.Id,
                    Address = station.Address,
                    Pos = new Position { Lat = station.Position.Latitude, Lon = station.Position.Longitude },
                };
                initData.Stations.Add(stationInit);

                foreach (var charger in station.Chargers)
                {
                    var chargerProto = new Charger
                    {
                        Id = charger.Id,
                        MaxPowerKw = charger.MaxPowerKW,
                        StationId = station.Id,
                        IsDual = charger is DualCharger,
                    };

                    initData.Chargers.Add(chargerProto);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error building initial engine data: {ex}");
        }

        return initData;
    }
}
