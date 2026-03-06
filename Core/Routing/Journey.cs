namespace Core.Routing;

using Core.Shared;

class Journey(int depature, Paths path, Position position)
{
    private Position _position = position; // 16 bytes
    public readonly int depature = depature;
    public required Paths Path { get; set; } = path;
}
