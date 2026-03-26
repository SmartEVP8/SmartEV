namespace Engine.Cost;

using System.Data;
using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Routing;

/// <summary>
/// Computes the cost of detouring to each station and selects the station with the lowest cost.
/// </summary>
/// <param name="costStore">The cost store.</param>
public class ComputeCost(ICostStore costStore)
{
    private readonly ICostStore _costStore = costStore;

    /// <summary>
    /// Computes the cost of detouring to each station and selects the station with the lowest cost.
    /// </summary>
    /// <param name="ev">The EV for which to compute costs.</param>
    /// <param name="stations">The array of stations to evaluate.</param>
    /// <param name="durations">The array of travel durations for each station.</param>
    /// <returns>The station with the lowest cost.</returns>
    /// <exception cref="NoNullAllowedException">If no suitable station is found.</exception>
    public Station Compute(ref EV ev, Station[] stations, float[] durations)
    {
        var bestCost = double.MaxValue;
        Station? bestStation = null;
        var weights = _costStore.GetWeights();

        for (var i = 0; i < stations.Length; i++)
        {
            var station = stations[i];
            var duration = durations[i];

            var effectiveQueueCost = CalculateEffectiveQueueSizeCost(station, weights);
            var pathDeviationCost = CalculatePathDeviationCost(ref ev, duration, weights);
            var urgencyCost = CalculateUrgencyCost(ref ev, weights);
            var priceCost = CalculatePriceCost(ref ev, station, weights);
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
            }
        }

        if (bestStation is null)
            throw new NoNullAllowedException("No station found in station map.");

        return bestStation;
    }

    // TODO: Think about effective queue size
    private static double CalculateEffectiveQueueSizeCost(Station station, CostWeights weights)
    {
        var totalQueueSize = station.Chargers.Sum(c => c.Queue.Count);
        var effectiveQueueSize = totalQueueSize / station.Chargers.Count; // Average queue size per charger

        return weights.EffectiveQueueSize * MathF.Pow(effectiveQueueSize, 2);
    }

    private static double CalculatePathDeviationCost(ref EV ev, float duration, CostWeights weights)
    {
        var pathDeviation = PathDeviator.CalculateDetourDeviation(ref ev, duration);
        return weights.PathDeviation * pathDeviation;
    }

    private static double CalculateUrgencyCost(ref EV ev, CostWeights weights)
    {
        var urgency = Urgency.CalculateChargeUrgency(ev.Battery.StateOfCharge, ev.Preferences.MinAcceptableCharge);
        return weights.Urgency * urgency;
    }

    private static double CalculatePriceCost(ref EV ev, Station station, CostWeights weights)
    {
        var price = station.Price;
        return weights.PriceSensitivity * ev.Preferences.PriceSensitivity * price;
    }

    // TODO: Implement
    private static double CalculateEffectiveWaitTimeCost(CostWeights weights) => weights.ExpectedWaitTime * 0;
}