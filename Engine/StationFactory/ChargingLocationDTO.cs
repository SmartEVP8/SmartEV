namespace Engine.StationFactory;

public class ChargingLocationDTO
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Town { get; set; } = "";
    public string Postcode { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}