namespace Core.Routing;

using Core.Shared;

class Journey(int depature, Path path, Position position)
{
    private Position _position = position; // 16 bytes
    public readonly int depature = depature;
    public required Path Path { get; set; } = path;
}
