namespace Engine.Grid;

using Core.Shared;

/// <summary>
/// The ISpatialGrid interface defines a contract for spatial indexing and querying of stations based on their geographic location.
/// </summary>
public interface ISpatialGrid
{
    /// <summary>
    /// Given a polyline (a list of waypoints) and a radius, return the list of station ids that are within the radius of any point along the polyline.
    /// </summary>
    /// <param name="waypoints">The polyline / list of waypoints.</param>
    /// <param name="radius">Radius to search around the polyline.</param>
    /// <returns>A lits of uints of stations id's.</returns>
    List<ushort> GetStationsAlongPolyline(List<Position> waypoints, double radius);
}
