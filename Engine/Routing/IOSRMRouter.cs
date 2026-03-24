namespace Engine.Routing;

using Core.Charging;

public interface IOSRMRouter : IMatrixRouter, IDisposable, IPointToPointRouter, IDestinationRouter
{
    void InitStations(List<Station> stations);

    (float[] durations, float[] distances) QueryStationsWithDest(
        double evLon,
        double evLat,
        double destLon,
        double destLat,
        ushort[] indices);

    (float[] durations, float[] distances) QueryStations(
        double evLon,
        double evLat,
        ushort[] indices);

    (float duration, string polyline) QuerySingleDestination(
        double evLon,
        double evLat,
        double destLon,
        double destLat);

    (float duration, string polyline) QueryDestination(
        double[] coords);

    (float[] durations, float[] distances) QueryPointsToPoints(
        double[] srcCoords,
        double[] dstCoords);
}
