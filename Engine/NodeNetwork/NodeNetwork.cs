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
        Console.WriteLine("Creating station and city nodes... Step 1");
        var stationNodes = _nodeFactory.CreateStationNodes();
        var cityNodes = _nodeFactory.CreateCityNodes();
        Nodes = [.. stationNodes, .. cityNodes];
    }

    public void createNodes()
    {
        Console.WriteLine("Adding Transitions on PNodes... Step 2");
        Nodes = _nodeFactory.AddAllTransitions(Nodes);
        Console.WriteLine("Removing duplicate transitions... Step 3");
        Nodes = _nodeFactory.RemoveDuplicateTransitions(Nodes);

        Console.WriteLine("Adding waypoints and removing duplicates... Step 4");
        Nodes = _nodeFactory.AddWaypoints(Nodes);

        // Write nodes.json to the project root (assume Engine/NodeNetwork/ is two levels below project root)
        var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../"));
        var fileToWrite = new FileInfo(Path.Combine(projectRoot, "nodes.json"));
        var json = JsonSerializer.Serialize(Nodes, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(fileToWrite.FullName, json);
        throw new Exception($"Node network creation completed and nodes.json written to {fileToWrite.FullName}.");
    }
}
