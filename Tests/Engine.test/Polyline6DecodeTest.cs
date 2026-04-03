namespace Engine.test;

using Engine.Utils;

public class Polyline6DecodeTests
{
    [Theory]
    [InlineData("_p~iF~ps|U_ulLnnqC_mqNvxq`@", 0, 38.5, -120.2)]
    [InlineData("_p~iF~ps|U_ulLnnqC_mqNvxq`@", 1, 40.7, -120.95)]
    [InlineData("_p~iF~ps|U_ulLnnqC_mqNvxq`@", 2, 43.252, -126.453)]
    public void DecodePolyline_GoogleSample_ReturnsCorrectPoint(
    string polyline, int index, double expectedLat, double expectedLng)
    {
        var res = Polyline6ToPoints.DecodePolyline(polyline);
        Assert.Equal(expectedLat, res[index].Latitude, precision: 5);
        Assert.Equal(expectedLng, res[index].Longitude, precision: 5);
    }
}
