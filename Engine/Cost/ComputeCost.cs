namespace Engine.Cost;

using System.Data;
using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Routing;
using Engine.Services;

/// <summary>
/// Computes the cost of detouring to each station and selects the station with the lowest cost.
/// </summary>
/// <param name="costStore">The cost store.</param>
public class ComputeCost(ICostStore costStore, StationService stationService)
{
    /// <summary>
    /// Computes the cost of detouring to each station and selects the station with the lowest cost.
    /// </summary>
    /// <param name="ev">The EV for which to compute costs.</param>
    /// <param name="stationDurations">A map of station ID to travel duration for each station.</param>
    /// <param name="time">The current time.</param>
    /// <returns>The station with the lowest cost.</returns>
    /// <exception cref="NoNullAllowedException">If no suitable station is found.</exception>
    public Station Compute(ref EV ev, Dictionary<ushort, float> stationDurations, Time time)
    {
        var bestCost = double.MaxValue;
        Station? bestStation = null;
        var weights = costStore.GetWeights();
        var bestQueueSize = -1d;
        var bestPath = -1d;
        var bestUrgency = -1d;
        var bestPrice = -1d;

        foreach (var (stationId, duration) in stationDurations)
        {
            var station = stationService.GetStation(stationId)
                ?? throw new NoNullAllowedException($"Station {stationId} not found.");
            var effectiveQueueCost = CalculateEffectiveQueueSizeCost(station, weights);
            var pathDeviationCost = CalculatePathDeviationCost(ref ev, duration, weights);
            var urgencyCost = CalculateUrgencyCost(ref ev, weights);
            var priceCost = CalculatePriceCost(ref ev, station, weights, time);
            var effectiveWaitTimeCost = CalculateEffectiveWaitTimeCost(weights);
            var cost = effectiveQueueCost
                + pathDeviationCost
                + urgencyCost
                + priceCost
                + effectiveWaitTimeCost;

            if (cost < bestCost)
            {
                bestCost = cost;
                bestStation = station;
                bestQueueSize = effectiveQueueCost;
                bestPath = pathDeviationCost;
                bestUrgency = urgencyCost;
                bestPrice = priceCost;
            }
        }

        if (bestStation is null)
            throw new NoNullAllowedException("No station found in station map.");

        Console.WriteLine($"[Selected station {bestStation.Id} with cost {bestCost} (Queue: {bestQueueSize}, Path: {bestPath}, Urgency: {bestUrgency}, Price: {bestPrice})");
        return bestStation;
    }

    // TODO: Think about effective queue size
    private float CalculateEffectiveQueueSizeCost(Station station, CostWeights weights)
    {
        var totalQueueSize = stationService.GetTotalQueueSize(station.Id);
        var effectiveQueueSize = (float)totalQueueSize / station.Chargers.Count;
        return weights.EffectiveQueueSize * MathF.Pow(effectiveQueueSize, 2);
    }

    private static float CalculatePathDeviationCost(ref EV ev, float duration, CostWeights weights)
    {
        var pathDeviation = PathDeviator.CalculateDetourDeviation(ref ev, duration);
        return weights.PathDeviation * pathDeviation;
    }

    private static double CalculateUrgencyCost(ref EV ev, CostWeights weights)
    {
        var urgency = Urgency.CalculateChargeUrgency(ev.Battery.StateOfCharge, ev.Preferences.MinAcceptableCharge);
        return weights.Urgency * urgency;
    }

    private static float CalculatePriceCost(ref EV ev, Station station, CostWeights weights, Time time)
    {
        var price = station.GetPrice(time);
        return weights.PriceSensitivity * ev.Preferences.PriceSensitivity * price;
    }

    // TODO: Implement
    private static float CalculateEffectiveWaitTimeCost(CostWeights weights) => weights.ExpectedWaitTime * 0;
}
