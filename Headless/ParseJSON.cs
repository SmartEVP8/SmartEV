namespace Headless;

/// <summary>
/// Parses the JSON to obtain the address info for the EV stations containing latitude and longitude.
/// </summary>
public class ParseJSON
{
    /// <summary>
    /// Gets or sets the latitude for a station.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude for a station.
    /// </summary>
    public double Longitude { get; set; }
}

/// <summary>
/// Represents the data structure for EV station information containing latitude and longitude.
/// </summary>
public class EvStationData
{
    /// <summary>
    /// Gets or sets the address information for the EV station, which includes latitude and longitude.
    /// </summary>
    required public ParseJSON AddressInfo { get; set; }
}