namespace Core.Shared;

public readonly struct Position(double longitude, double latitude)
{
    public readonly double Longitude = longitude;
    public readonly double Latitude = latitude;
}
