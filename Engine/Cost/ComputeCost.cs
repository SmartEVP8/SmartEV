namespace Engine.Cost;

using System.Data;
using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Services;
using Core.Helper;
using Engine.Events.Middleware;

/// <summary>
/// Computes the cost of detouring to each station and selects the station with the lowest cost.
/// </summary>
/// <param name="costStore">The cost store.</param>
/// <param name="stationService">The station service.</param>
/// <param name="energyPrices">The energy prices.</param>
public class CostFunction(ICostStore costStore, IStationService stationService, EnergyPrices energyPrices)
{
    /// <summary>
    /// Computes the cost of detouring to each station and selects the station with the lowest cost.
    /// </summary>
    /// <param name="ev">The EV for which to compute costs.</param>
    /// <param name="stationDurations">A map of station ID to travel duration for each station.</param>
    /// <param name="time">The current time.</param>
    /// <returns>The station with the lowest cost.</returns>
    /// <exception cref="NoNullAllowedException">If no suitable station is found.</exception>
    public Station Compute(ref EV ev, Dictionary<ushort, DurToStationAndDest> stationDurations, Time time)
    {
        var bestCost = double.MaxValue;
        var weights = costStore.GetWeights() ?? throw Log.Error(0, time, new NoNullAllowedException("Cost weights not found in store."), ("EV", ev));
        Station? bestStation = null;

        foreach (var (stationId, durations) in stationDurations)
        {
            var station = stationService.GetStation(stationId);

            var effectiveQueueCost = CalculateEffectiveQueueSizeCost(station, weights);
            var pathDeviationCost = CalculatePathDeviationCost(ref ev, durations.DurToDest + durations.DurToStation, weights, time);
            var urgencyCost = Urgency.CalculateChargeUrgency(ref ev, (uint)durations.DurToStation);
            var priceCost = CalculatePriceCost(ref ev, station, weights, time, energyPrices);
            var effectiveWaitTimeCost = CalculateEffectiveWaitTimeCost(weights);
            var cost = (1 - urgencyCost) * (effectiveQueueCost + pathDeviationCost + priceCost + effectiveWaitTimeCost);

            if (double.IsNaN(cost))
            {
                throw Log.Error(0, time, new InvalidOperationException(
                    $"Invalid cost calculated for station {stationId}: {cost}. " +
                    $"Queue={effectiveQueueCost}, PathDev={pathDeviationCost}, " +
                    $"Urgency={urgencyCost}, Price={priceCost}, Wait={effectiveWaitTimeCost}"));
            }

            if (cost < bestCost)
            {
                bestCost = cost;
                bestStation = station;
            }
        }

        if (bestStation is null)
        {
            throw Log.Error(0, time, new ArgumentNullException("No suitable station found."), ("EV", ev));
        }

        return bestStation;
    }

    // TODO: Think about effective queue size
    private float CalculateEffectiveQueueSizeCost(Station station, CostWeights weights)
    {
        if (station.Chargers.Count == 0)
            throw Log.Error(0, 0, new InvalidOperationException($"Station {station.Id} has no chargers."), ("StationId", station.Id));

        var totalQueueSize = stationService.GetStation(station.Id)
                                .Chargers.Sum(cs => cs.Queue.Count);
        if (totalQueueSize == 0)
            return 0;

        var effectiveQueueSize = (float)totalQueueSize / station.Chargers.Count;
        return weights.EffectiveQueueSize * MathF.Pow(effectiveQueueSize, 3);
    }

    /// <summary>
    /// Calculates the path deviation cost based on the detour duration compared to the remaining original journey duration.
    /// </summary>
    /// <returns>
    /// The path deviation cost in minutes.
    /// </returns>
    private static float CalculatePathDeviationCost(ref EV ev, float detourDuration, CostWeights weights, Time time)
    {
        var remainingCurrentRoute = ev.Journey.RemainingCurrentRoute(time);
        if (remainingCurrentRoute <= 0)
            throw Log.Error(0, time, new InvalidOperationException($"EV {ev} has no remaining route duration, cannot calculate path deviation cost."), ("EV", ev));

        var extraTimeCostMilliseconds = detourDuration - remainingCurrentRoute;
        if (extraTimeCostMilliseconds <= 0)
            return 0;

        var extraTimeCostMinutes = extraTimeCostMilliseconds / Time.MillisecondsPerMinute;
        return weights.PathDeviation * Math.Clamp(extraTimeCostMinutes, 0, float.MaxValue);
    }

    private static float CalculatePriceCost(ref EV ev, Station station, CostWeights weights, Time time, EnergyPrices energyPrices)
    {
        var currentPrice = station.GetPrice(time);
        var lowestPrice = energyPrices.GetLowestPrice();
        return weights.PriceSensitivity * ev.Preferences.PriceSensitivity * (currentPrice - lowestPrice) * 100; // Scale factor to convert price difference to a comparable cost value
    }

    // TODO: Implement
    private static float CalculateEffectiveWaitTimeCost(CostWeights weights) => weights.ExpectedWaitTime * 0;
}
