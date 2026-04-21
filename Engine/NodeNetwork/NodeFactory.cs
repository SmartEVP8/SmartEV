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

            var result = _router.QueryPointsToPoints(
                [nodes[i].Position.Longitude, nodes[i].Position.Latitude],
                [.. nodes.Where((_, index) => index != i).Select(n => new double[] { n.Position.Longitude, n.Position.Latitude }).SelectMany(a => a)]
            );

            if (result == null)
                throw new Exception($"Router query result was null for node index {i}.");
            if (result.Durations == null || result.Distances == null)
                throw new Exception($"Router query returned null durations or distances for node index {i}.");
            if (result.Durations.Length != nodes.Length - 1 || result.Distances.Length != nodes.Length - 1)
                throw new Exception($"Router query returned unexpected array lengths for node index {i}.");

            for (var j = 0; j < result.Durations.Length; j++)
            {
                if (result.Durations[j] < 0 || result.Distances[j] < 0)
                    continue;

                uint toNodeId = (uint)(j >= i ? j + 1 : j);
                transitions[transitionIndex++] = new Transition(toNodeId, new Edge((uint)result.Durations[j], result.Distances[j]));
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
            var filteredTransitions = new List<Transition>(originalTransitions);

            foreach (var transition in originalTransitions)
            {
                var toNodeId = transition.nodeId;
                var edge = transition.Edge;

                foreach (var otherTransition in originalTransitions)
                {
                    if (GeoMath.IsOnSegment(nodes[toNodeId].Position, nodes[otherTransition.nodeId].Position, nodes[i].Position))
                    {
                        // If the edge to the other node is shorter than the edge to the current node, remove the current edge
                        if (otherTransition.Edge.Distance < edge.Distance)
                        {
                            // Remove the current edge if it's still in the list
                            filteredTransitions.RemoveAll(t => t.nodeId == toNodeId);
                        }
                    }
                }
            }
            newNodes[i] = new Node(nodes[i].Position, filteredTransitions.ToArray()) { Id = (uint)i };
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
                var result = _router.QuerySingleDestination(
                    node.Position.Longitude, node.Position.Latitude,
                    nodes[toNodeId].Position.Longitude, nodes[toNodeId].Position.Latitude
                );
                if (result == null)
                    throw new Exception($"Router single destination query returned null for node at {node.Position} to {nodes[toNodeId].Position}.");
                if (string.IsNullOrEmpty(result.Polyline))
                    throw new Exception($"Router single destination query returned null or empty polyline for node at {node.Position} to {nodes[toNodeId].Position}.");

                var decodedPolyline = Polyline6ToPoints.DecodePolyline(result.Polyline);
                if (decodedPolyline == null)
                    throw new Exception($"Polyline decoding returned null for node at {node.Position} to {nodes[toNodeId].Position}.");
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
