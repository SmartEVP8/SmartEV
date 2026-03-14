namespace Core.Routing;

using Core.Shared;

class Journey(int depature, Paths path)
{
    public readonly int depature = depature;
    public required Paths Path { get; set; } = path;
}
