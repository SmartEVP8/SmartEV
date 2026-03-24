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
/// <param name="pathDeviator">The path deviator.</param>
public class ComputeCost(ICostStore costStore, PathDeviator pathDeviator)
{
    private readonly ICostStore _costStore = costStore;
    private readonly PathDeviator _pathDeviator = pathDeviator;

    /// <summary>
    /// Computes the cost of detouring to each station and selects the station with the lowest cost.
    /// </summary>
    /// <param name="ev">The EV for which to compute costs.</param>
    /// <param name="stations">The array of stations to evaluate.</param>
    /// <param name="journeys">The journeys for each station.</param>
    /// <param name="currentTime">The current time.</param>
    /// <returns>The station with the lowest cost.</returns>
    /// <exception cref="NoNullAllowedException">If no suitable station is found.</exception>
    public Station Compute(ref EV ev, Station[] stations, (float[] duration, float[] distance, string[] polyline) journeys, Time currentTime)
    {
        var totalCost = 0d;
        Station? bestStation = null;

        for (var i = 0; i < stations.Length; i++)
        {
            var tempCost = 0d;
            var station = stations[i];
            var (duration, distance, polyline) = (journeys.duration[i], journeys.distance[i], journeys.polyline[i]);

            // Factors
            var effectiveQueueSize = station.Chargers.Sum(c => c.Queue.Count);
            var (totalChargers, availableChargers) = (
                station.Chargers.Count,
                station.Chargers.Where(c => c.Queue.Count == 0).ToList().Count
            );
            var availableChargerRatio = MathF.Abs((availableChargers / totalChargers) - 1);
            var pathDeviation = _pathDeviator.CalculateDetourDeviation(ev.Journey, currentTime, station.Position);
            var urgency = Urgency.CalculateChargeUrgency(ev.Battery.StateOfCharge, ev.Preferences.MinAcceptableCharge);
            var price = station.Price;

            // Costs
            var weights = _costStore.GetWeights();

            var effectiveQueueCost = weights.EffectiveQueueSize * MathF.Pow(effectiveQueueSize, 2);
            var pathDeviationCost = weights.PathDeviation * pathDeviation.Item1;
            var urgencyCost = weights.Urgency * urgency;
            var priceCost = weights.PriceSensitivity * ev.Preferences.PriceSensitivity * price;
            var effectiveWaitTimeCost = weights.ExpectedWaitTime * 0; // TODO: Compute expected wait time cost
            var availableChargerRatioCost = weights.AvailableChargerRatio * availableChargerRatio;

            tempCost = effectiveQueueCost + pathDeviationCost + urgencyCost + priceCost + effectiveWaitTimeCost + availableChargerRatioCost;
            if (tempCost > totalCost)
            {
                totalCost = tempCost;
                bestStation = station;
            }
        }

        if (bestStation is null)
            throw new NoNullAllowedException("No station found in station map.");

        return bestStation;
    }
}