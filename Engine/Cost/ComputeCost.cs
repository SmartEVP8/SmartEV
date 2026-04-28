namespace Engine.Cost;

using System.Data;
using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Services;
using Serilog;
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
        var weights = costStore.GetWeights();
        if (weights is null)
        {
            var ex = new ArgumentNullException("Cost weights not found in store.");
            Log.Error(ex, "Cost weights not found in store. Cannot compute cost for EV {@EV}", ev);
            throw ex;
        }

        Station? bestStation = null;
        var lowestPrice = energyPrices.GetLowestPrice();

        foreach (var (stationId, durations) in stationDurations)
        {
            var station = stationService.GetStation(stationId);

            var pathDeviationCost = CalculatePathDeviationCost(ref ev, durations.DurToDest + durations.DurToStation, weights, time);
            var urgencyCost = Urgency.CalculateChargeUrgency(ref ev, durations.DistToStationMeters);
            var priceCost = CalculatePriceCost(ref ev, station, weights, time, lowestPrice);
            var effectiveWaitTimeCost = CalculateEffectiveWaitTimeCost(weights, time, durations.DurToStation, stationId);
            var cost = (1 - urgencyCost) * (pathDeviationCost + priceCost + effectiveWaitTimeCost);

            if (double.IsNaN(cost))
            {
                var ex = new InvalidOperationException($"Calculated cost is NaN for station {stationId}. PathDev={pathDeviationCost}, Urgency={urgencyCost}, Price={priceCost}, Wait={effectiveWaitTimeCost}");
                Log.Error(ex, "Calculated cost is NaN for station {StationId}. PathDeviationCost={PathDeviationCost}, UrgencyCost={UrgencyCost}, PriceCost={PriceCost}, EffectiveWaitTimeCost={EffectiveWaitTimeCost}",
                    stationId, pathDeviationCost, urgencyCost, priceCost, effectiveWaitTimeCost);
                throw ex;
            }

            if (cost < bestCost)
            {
                bestCost = cost;
                bestStation = station;
            }
        }

        if (bestStation is null)
        {
            var ex = new ArgumentNullException("No suitable station found.");
            Log.Error(ex, "No suitable station found for EV {@EV} with station durations: {StationDurations}", ev, stationDurations);
            throw ex;
        }

        return bestStation;
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
        {
            var ex = new InvalidOperationException($"EV {ev} has no remaining route duration, cannot calculate path deviation cost.");
            Log.Error(ex, "EV {@EV} has no remaining route duration at time {@Time}, cannot calculate path deviation cost.", ev, time);
            throw ex;
        }

        var extraTimeCostMilliseconds = detourDuration - remainingCurrentRoute;
        if (extraTimeCostMilliseconds <= 0)
            return 0;

        var extraTimeCostMinutes = extraTimeCostMilliseconds / Time.MillisecondsPerMinute;
        return weights.PathDeviation * Math.Clamp(extraTimeCostMinutes, 0, float.MaxValue);
    }

    private static float CalculatePriceCost(ref EV ev, Station station, CostWeights weights, Time time, float lowestPrice)
    {
        var currentPrice = station.GetPrice(time);
        return weights.PriceSensitivity * ev.Preferences.PriceSensitivity * (currentPrice - lowestPrice);
    }

    private float CalculateEffectiveWaitTimeCost(CostWeights weights, Time time, float duration, ushort stationId)
    {
        var expectedArrival = (Time)(uint)Math.Ceiling(time + duration);
        var availableAt = stationService.ExpectedWaitTime(stationId, time, expectedArrival);
        var wait = availableAt - expectedArrival;
        var waitMinutes = Math.Max(0, wait.Minutes);
        return weights.ExpectedWaitTime * waitMinutes;
    }
}
