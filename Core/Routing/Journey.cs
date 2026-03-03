namespace Core.Routing;

using Core.Shared;

class Journey(int depature, Path path)
{
    public readonly int depature = depature;
    public required Path Path { get; set; } = path;
}
