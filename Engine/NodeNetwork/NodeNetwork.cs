using Engine.Spawning;
using System.Text.Json;
public class NodeNetwork
{
    private readonly NodeFactory _nodeFactory;
    public Node[] Nodes { get; set; }

    public NodeNetwork(NodeFactory nodeFactory)
    {
        _nodeFactory = nodeFactory;
    }

    public void CreateNodeNetwork()
    {
        var stationNodes = _nodeFactory.CreateStationNodes();
        var cityNodes = _nodeFactory.CreateCityNodes();
        Nodes = [.. stationNodes, .. cityNodes];
    }

    public void createNodes()
    {
        Nodes = _nodeFactory.AddAllTransitions(Nodes);
        Nodes = _nodeFactory.RemoveDuplicateTransitions(Nodes);
        Nodes = _nodeFactory.AddWaypoints(Nodes);

        var fileToWrite = new FileInfo("nodes.json");
        var json = JsonSerializer.Serialize(Nodes, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(fileToWrite.FullName, json);
    }
}
