namespace Core.Shared;

public readonly struct Position(double latitude, double longitude)
{
    public readonly double Latitude = latitude;
    public readonly double Longitude = longitude;
}
