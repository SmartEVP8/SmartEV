using Core.Shared;

public class Node
{
    public Position Position { get; }
    public Transition[] Transitions { get; set; }
    public uint Id { get; set; }

    public Node(Position position, Transition[] transitions)
    {
        Position = position;
        Transitions = transitions;
    }
}

public struct Transition
{
    public uint nodeId { get; }
    public Edge Edge { get; }

    public Transition(uint nodeId, Edge edge)
    {
        this.nodeId = nodeId;
        Edge = edge;
    }
}
