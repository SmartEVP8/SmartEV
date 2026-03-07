using System;
using Engine;
using Xunit;

namespace Engine.Tests;

public class CalculateJourneyTests
{
    [Fact]
    public void CalculateDistance_Should_()
    {
        // Arrange
        var sut = new CalculateJourney();

        // Act
        var result = sut.CalculateDistance(default, default, default);

        // Assert
        Assert.NotNull(result);
    }
}
