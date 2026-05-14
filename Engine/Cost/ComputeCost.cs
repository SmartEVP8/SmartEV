namespace Engine.Cost;

using System.Data;
using Core.Charging;
using Core.Charging.ChargingModel;
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
            Log.Error("Cost weights not found in store. Cannot compute cost for EV {@EV}", ev);
            throw new ArgumentNullException("Cost weights not found in store.");
        }

        Station? bestStation = null;
        var lowestPrice = energyPrices.GetLowestPrice();

        foreach (var (stationId, durations) in stationDurations)
        {
            var station = stationService.GetStation(stationId);

            var pathDeviationCost = CalculatePathDeviationCost(ref ev, durations.DurToDest + durations.DurToStation, weights, time);
            var urgencyCost = CalculateChargeUrgency(ref ev, durations.DistToStationMeters);
            var priceCost = CalculatePriceCost(ref ev, station, weights, time, lowestPrice);
            var effectiveWaitTimeCost = CalculateEffectiveWaitTimeCost(weights, time, durations.DurToStation, stationId, ref ev);
            var cost = (1 - urgencyCost) * (pathDeviationCost + priceCost + effectiveWaitTimeCost);

            if (double.IsNaN(cost))
            {
                Log.Error(
                    "Calculated cost is NaN for station {StationId}. PathDeviationCost={PathDeviationCost}, UrgencyCost={UrgencyCost}, PriceCost={PriceCost}, EffectiveWaitTimeCost={EffectiveWaitTimeCost}",
                    stationId, pathDeviationCost, urgencyCost, priceCost, effectiveWaitTimeCost);
                throw new InvalidOperationException($"Calculated cost is NaN for station {stationId}. PathDev={pathDeviationCost}, Urgency={urgencyCost}, Price={priceCost}, Wait={effectiveWaitTimeCost}");
            }

            if (cost < bestCost)
            {
                bestCost = cost;
                bestStation = station;
            }
        }

        if (bestStation is null)
        {
            Log.Error("No suitable station found for EV {@EV} with station durations: {StationDurations}", ev, stationDurations);
            throw new ArgumentNullException("No suitable station found.");
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
            Log.Error("EV {@EV} has no remaining route duration at time {@Time}, cannot calculate path deviation cost.", ev, time);
            throw new InvalidOperationException($"EV {ev} has no remaining route duration, cannot calculate path deviation cost.");
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

    private float CalculateEffectiveWaitTimeCost(CostWeights weights, Time time, float duration, ushort stationId, ref EV ev)
    {
        var expectedArrival = (Time)(uint)Math.Ceiling(time + duration);
        var currentSoC = ev.Battery.CurrentChargeKWh / ev.Battery.MaxCapacityKWh;
        var targetSoC = Math.Max(currentSoC + 0.2, 0.8);

        var connectedEV = new ConnectedEV(
            ev.Id,
            currentSoC,
            targetSoC,
            ev.Battery.MaxCapacityKWh,
            ev.Battery.MaxChargeRateKW,
            expectedArrival);

        var availableAt = stationService.ExpectedWaitTime(stationId, time, expectedArrival, connectedEV);
        var waitMilliseconds = availableAt > expectedArrival ? availableAt - expectedArrival : new Time(0);
        var waitMinutes = waitMilliseconds / Time.MillisecondsPerMinute;
        return weights.ExpectedWaitTime * waitMinutes;
    }

    /// <summary>
    /// Calculates the urgency of charging based on the state of charge (SoC) of the battery at arrival at
    /// and a minimum acceptable charge level.
    /// </summary>
    /// <param name="ev">The EV for which to calculate urgency.</param>
    /// <param name="distanceToStationMeter">The estimated distance to reach the station, used to estimate SoC at arrival.</param>
    /// <returns>
    /// The urgency of charging as a value between 0 and 1, where a higher value indicates a more urgent need for charging.
    /// </returns>
    private static double CalculateChargeUrgency(ref EV ev, float distanceToStationMeter)
    {
        const double upperChargeLimit = 0.80;

        var soc = (ev.Battery.CurrentChargeKWh - ev.EnergyForDistanceKWh(distanceToStationMeter / 1000f)) / ev.Battery.MaxCapacityKWh;

        if (soc <= 0)
            return 1 - float.MaxValue;

        if (soc >= upperChargeLimit)
            return 0.0;

        if (soc <= ev.Preferences.MinAcceptableCharge)
            return 0.9999;

        // Examples:
        // Soc = 0.79 => Urgency = 1 - (1 / 0.8^2 * 0.79^2) = 1 - (1 / 0.64 * 0.6241) = 1 - 0.97515625 = 0.02484375
        // SoC = 0.2 => Urgency = 1 - (1 / 0.8^2 * 0.2^2) = 1 - (1 / 0.64 * 0.04) = 1 - 0.0625 = 0.9375
        return 1 - (1 / Math.Pow(upperChargeLimit, 2) * Math.Pow(soc, 2));
    }
}
