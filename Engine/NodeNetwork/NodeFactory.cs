using System.Text.Json;
using Core.Helper;
using Engine.StationFactory;
using Core.Shared;
using Engine.Spawning;
using Engine.Routing;
using Core.GeoMath;
using Engine.Utils;
public class NodeFactory
{
    private readonly FileInfo _stationsFile;
    private readonly List<City> _city;
    private readonly IOSRMRouter _router;

    public NodeFactory(FileInfo stationsFile, List<City> cities, IOSRMRouter router)
    {
        _stationsFile = stationsFile;
        _city = cities;
        _router = router;
    }

    public Node[] CreateStationNodes()
    {
        var json = File.ReadAllText(_stationsFile.FullName);
        var locations = JsonSerializer.Deserialize<List<StationLocationDTO>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw Log.Error(0, 0, new InvalidOperationException("JSON file was empty or null."));

        if (locations.Count == 0)
            throw Log.Error(0, 0, new InvalidOperationException("Station locations JSON file was empty."));

        var stationNodes = new Node[locations.Count];
        for (var i = 0; i < locations.Count; i++)
        {
            var position = new Position(locations[i].Longitude, locations[i].Latitude);
            stationNodes[i] = new Node(position, []);
        }

        return stationNodes;
    }

    public Node[] CreateCityNodes()
    {
        var cityNodes = new Node[_city.Count];
        for (var i = 0; i < _city.Count; i++)
        {
            var position = _city[i].Position;
            cityNodes[i] = new Node(position, []);
        }

        return cityNodes;
    }

    public Node[] AddAllTransitions(Node[] nodes)
    {
        var newNodes = new Node[nodes.Length];
        var amountofNodes = nodes.Length;
        var counter = 0;
        Console.WriteLine($"Starting to add transitions for {amountofNodes} nodes...");
        Parallel.For(0, nodes.Length, i =>
        {
            var transitions = new Transition[nodes.Length - 1];
            var transitionIndex = 0;
            for (var j = 0; j < nodes.Length; j++)
            {
                if (i == j)
                    continue;
                var result = _router.QuerySingleDestination(
                               nodes[i].Position.Longitude, nodes[i].Position.Latitude,
                               nodes[j].Position.Longitude, nodes[j].Position.Latitude
                           );

                if (result == null)
                    throw new Exception($"Router query result was null for node index {i}.");

                uint toNodeId = (uint)(j >= i ? j + 1 : j);
                transitions[transitionIndex++] = new Transition(toNodeId, new Edge((uint)result.Duration, result.Distance));
            }

            // Resize the transitions array to the actual number of valid transitions
            Array.Resize(ref transitions, transitionIndex);
            newNodes[i] = new Node(nodes[i].Position, transitions) { Id = (uint)i };
            counter++;
            if (counter % 100 == 0 || counter == amountofNodes)
            {
                var progress = "";
                var tempCounter = counter;
                while (tempCounter >= 1000)
                {
                    progress = "=" + progress;
                    tempCounter -= amountofNodes / 10;
                }

                progress += $">{counter / (float)amountofNodes * 100}%";

                Console.WriteLine($"{progress}");
            }
        });
        return newNodes;
    }

    public Node[] RemoveDuplicateTransitions(Node[] nodes)
    {
        var newNodes = new Node[nodes.Length];
        Parallel.For(0, nodes.Length, i =>
        {
            var originalTransitions = nodes[i].Transitions;
            var filteredTransitions = new List<Transition>();

            foreach (var transition in originalTransitions)
            {
                var toNodeId = transition.nodeId;
                var edge = transition.Edge;

                // Lookup destination node by Id
                var toNode = nodes.FirstOrDefault(n => n.Id == toNodeId);
                if (toNode == null)
                {
                    // Don't add this transition, but keep the node
                    continue;
                }

                bool shouldRemove = false;
                foreach (var otherTransition in originalTransitions)
                {
                    var otherToNode = nodes.FirstOrDefault(n => n.Id == otherTransition.nodeId);
                    if (otherToNode == null)
                        continue;

                    if (GeoMath.IsOnSegment(toNode.Position, otherToNode.Position, nodes[i].Position))
                    {
                        // If the edge to the other node is shorter than the edge to the current node, mark for removal
                        if (otherTransition.Edge.Distance < edge.Distance)
                        {
                            shouldRemove = true;
                            break;
                        }
                    }
                }
                if (!shouldRemove)
                {
                    filteredTransitions.Add(transition);
                }
            }
            newNodes[i] = new Node(nodes[i].Position, filteredTransitions.ToArray()) { Id = nodes[i].Id };
        });
        return newNodes;
    }

    public Node[] AddWaypoints(Node[] nodes)
    {
        // Guard: Check for null Transitions
        foreach (var n in nodes)
        {
            if (n.Transitions == null)
                throw new NullReferenceException($"Node at position {n.Position} has null Transitions array.");
        }

        var newNodes = new List<Node>(nodes);
        var amountofNodes = nodes.Sum(n => n.Transitions.Length);
        var counter = 0;
        Console.WriteLine($"Starting to add waypoints for {amountofNodes} nodes...");
        Console.WriteLine($"Sum of transitions across all nodes before adding waypoints: {nodes.Sum(n => n.Transitions.Length)}");
        Parallel.ForEach(nodes, node =>
        {
            if (node.Transitions == null)
                throw new NullReferenceException($"Node at position {node.Position} has null Transitions array (inside parallel loop).");
            foreach (var transition in node.Transitions)
            {
                var toNodeId = transition.nodeId;
                // Find toNode by Id
                var toNode = nodes.FirstOrDefault(n => n.Id == toNodeId);
                if (toNode == null)
                {
                    Console.WriteLine($"[Warning] Could not find destination node with ID {toNodeId} for transition from node at {node.Position}. Skipping this transition.");
                    continue;
                }
                var result = _router.QuerySingleDestination(
                    node.Position.Longitude, node.Position.Latitude,
                    toNode.Position.Longitude, toNode.Position.Latitude
                );
                if (result == null)
                {
                    Console.WriteLine($"[Warning] Router single destination query returned null for node at {node.Position} to {toNode.Position}. Skipping this transition.");
                    continue;
                }
                if (string.IsNullOrEmpty(result.Polyline))
                {
                    Console.WriteLine($"[Warning] Router single destination query returned null or empty polyline for node at {node.Position} to {toNode.Position}. Skipping this transition.");
                    continue;
                }

                var decodedPolyline = Polyline6ToPoints.DecodePolyline(result.Polyline);
                if (decodedPolyline == null)
                {
                    Console.WriteLine($"[Warning] Polyline decoding returned null for node at {node.Position} to {toNode.Position}. Skipping this transition.");
                    continue;
                }
                foreach (var waypoint in decodedPolyline)
                {
                    var position = new Position(waypoint.Longitude, waypoint.Latitude);
                    var waypointNode = new Node(position, []);
                    if (!newNodes.Any(n => n.Position.Equals(position)))
                        newNodes.Add(waypointNode);
                }
                counter++;
                if (counter % 100 == 0 || counter == amountofNodes)
                {
                    var progress = "";
                    var tempCounter = counter;
                    while (tempCounter >= 1000)
                    {
                        progress = "=" + progress;
                        tempCounter -= amountofNodes / 10;
                    }
                    progress += $">{counter / (float)amountofNodes * 100}%";

                    Console.WriteLine($"{progress}");
                }
            }
        });
        Console.WriteLine($"Finished adding waypoints. Total nodes before removing duplicates and adding transitions: {newNodes.Count}");
        var resultWithTransitions = AddAllTransitions([.. newNodes]);
        Console.WriteLine($"Finished adding transitions to waypoints. Total nodes before removing duplicate transitions: {resultWithTransitions.Length}");
        var resultPruned = RemoveDuplicateTransitions(resultWithTransitions);
        return resultPruned;
    }
}
