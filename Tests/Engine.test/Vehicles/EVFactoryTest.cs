using Core.Vehicles;
using Engine.Vehicles;

/// <summary>
/// Tests for the EVFactory class.
/// </summary>
public class EVFactoryTest
{
    /// <summary>
    /// Verifies that the price sensitivity stays within [0, 1).
    /// </summary>
    [Fact]
    public void Create_PriceSensitivityWithinUnitRange()
    {
        var factory = MakeFactory();

        for (var i = 0; i < 20; i++)
        {
            var ev = factory.Create();
            Assert.InRange(ev.Preferences.PriceSensitivity, 0f, 1f);
        }
    }

    /// <summary>
    /// Initialization of an EVFactory.
    /// </summary>
    /// <param name="seed">The seed used to create EVs.</param>
    /// <returns>An EVFactory.</returns>
    private static EVFactory MakeFactory(int seed = 42) => new(new Random(seed));
}
