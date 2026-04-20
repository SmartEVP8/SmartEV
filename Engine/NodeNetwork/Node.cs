using Core.Shared;

public class Node
{
    public Position Position { get; }
    public Transition[] Transitions { get; set; }

    public Node(Position position, Transition[] transitions)
    {
        Position = position;
        Transitions = transitions;
    }
}

public struct Transition
{
    public Node To { get; }
    public Edge Edge { get; }

    public Transition(Node to, Edge edge)
    {
        To = to;
        Edge = edge;
    }
}
