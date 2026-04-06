namespace Core.test.Shared;

using Core.Shared;

public class PositionTests
{
    [Fact]
    public void PositionTolerance_WithinThreshold_AreEqual()
    {
        var pos1 = new Position(55.6761, 12.5683);
        var pos2 = new Position(55.67610001, 12.56830001);

        Assert.Equal(pos1, pos2);
    }

    [Fact]
    public void PositionTolerance_OutsideThreshold_NotEqual()
    {
        var pos1 = new Position(55.6761, 12.5683);
        var pos2 = new Position(56.0, 13.0);

        Assert.NotEqual(pos1, pos2);
    }
}
