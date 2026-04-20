public struct Edge
{
    public uint Duration { get; }
    public float Distance { get; }

    public Edge(uint duration, float distance)
    {
        Duration = duration;
        Distance = distance;
    }
}
