namespace Simulation
{
    public class AddressInfo
    {
        public double Latitude { get; set; }

        public double Longitude { get; set; }
    }

    public class EvStationData
    {
        required public AddressInfo AddressInfo { get; set; }
    }

}
