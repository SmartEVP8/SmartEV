namespace Engine.StationFactory;

/// <summary>
/// DTO representing the location of a station.
/// </summary>
public class StationLocationDTO
{
    /// <summary> Gets or sets the name of the station location. </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary> Gets or sets the address of the station location. </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary> Gets or sets the town of the station location. </summary>
    public string Town { get; set; } = string.Empty;

    /// <summary> Gets or sets the postcode of the station location. </summary>
    public string Postcode { get; set; } = string.Empty;

    /// <summary> Gets or sets the latitude of the station location. </summary>
    public double Latitude { get; set; }

    /// <summary> Gets or sets the longitude of the station location. </summary>
    public double Longitude { get; set; }
}