namespace Engine.test.Vehicles;

using Core.Shared;
using Engine.Grid;
using Engine.Routing;
using Engine.Spawning;
using Engine.Vehicles;

/// <summary>
/// Tests for the EVFactory class.
/// </summary>
public class EVFactoryTest
{
    /// <summary>
    /// Initialization of an EVFactory.
    /// </summary>
    /// <param name="seed">The seed used to create EVs.</param>
    /// <returns>An EVFactory.</returns>
    private static EVFactory MakeFactory(int seed = 42) => new(new Random(seed), new FakeJourneySamplerProvider(), new FakeRouter());

    private class FakeJourneySamplerProvider : IJourneySamplerProvider
    {
        private class FakeJourneySampler : IJourneySampler
        {
            public (Position Source, Position Destination) SampleSourceToDest(Random random) => (new Position(0, 0), new Position(0, 0));
        }

        private readonly FakeJourneySampler _fjs = new();

        public IJourneySampler Current => _fjs;

        public IJourneySampler Recompute(float scalar) => _fjs;
    }

    private class FakeRouter : IPointToPointRouter
    {
        public (float duration, string polyline) QuerySingleDestination(double evLon, double evLat, double destLon, double destLat) => (10, "_p~iF~ps|U_ulLnnqC_mqNvxq`@");
    }

    /// <summary>
    /// Verifies that the price sensitivity stays within [0, 1].
    /// </summary>
    [Fact]
    public void Create_PriceSensitivityWithinUnitRange()
    {
        var factory = MakeFactory();

        for (var i = 0; i < 20; i++)
        {
            var ev = factory.Create(0);
            Assert.InRange(ev.Preferences.PriceSensitivity, 0f, 1f);
        }
    }
}
