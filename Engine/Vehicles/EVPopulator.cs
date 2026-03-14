namespace Engine.Vehicles;

using Engine.Grid;
using Engine.Routing;

public class EVPopulator(Random random, int totalCapacity, IJourneySampler samplers, IPointToPointRouter pointToPointRouter)
{
    private readonly EVFactory _eVFactory = new(random);

    // Im not quite sure where this should live but potentially take this via DI.
    private readonly EVStore _eVStore = new(totalCapacity);
    private readonly IJourneySampler _samplers = samplers;
    private readonly IPointToPointRouter _ptpRouter = pointToPointRouter;

    // My thought is that we simply call this, potentially with a timeframe
    // and schedule events evenly into the future.
    public void CreateEVs(int amount)
    {
        var journeys = Enumerable.Range(0, amount)
            .Select(_ => _samplers.SampleSourceToDest(random))
            .ToArray();

        var sources = journeys.Select(j => j.Source).ToArray();
        var destinations = journeys.Select(j => j.Destination).ToArray();

        var routes = new (float duration, string polyline)[amount];
        Parallel.For(0, amount, i =>
        {
            var (source, destination) = journeys[i];
            routes[i] = _ptpRouter.QuerySingleDestination(
                source.Longitude,
                source.Latitude,
                destination.Longitude,
                destination.Latitude);
        });

        _eVStore.TryAllocate(amount, (index, ref ev) =>
        {
            ev = _eVFactory.Create();
        });

        // Figure out how to populate journey here
    }
}
