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
        for (var i = 0; i < nodes.Length; i++)
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

                if (result.Duration < 0 || result.Distance < 0)
                    continue;

                transitions[transitionIndex++] = new Transition(nodes[j], new Edge((uint)result.Duration, result.Distance));
            }

            // Resize the transitions array to the actual number of valid transitions
            Array.Resize(ref transitions, transitionIndex);
            newNodes[i] = new Node(nodes[i].Position, transitions);
        }
        return newNodes;
    }

    public Node[] RemoveDuplicateTransitions(Node[] nodes)
    {
        var newNodes = new Node[nodes.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            //If edges overlap remove the one that isnt neccesary if the node can reach another node through a thrid node
            foreach (var transition in nodes[i].Transitions)
            {
                var toNode = transition.To;
                var edge = transition.Edge;

                foreach (var otherTransition in nodes[i].Transitions)
                {
                    if (GeoMath.IsOnSegment(toNode.Position, otherTransition.To.Position, nodes[i].Position))
                    {
                        // If the edge to the other node is shorter than the edge to the current node, remove the current edge
                        if (otherTransition.Edge.Distance < edge.Distance)
                        {
                            // Remove the current edge
                            newNodes[i] = new Node(nodes[i].Position, [.. nodes[i].Transitions.Where(t => t.To != toNode)]);
                        }
                    }
                }
            }
        }
        return newNodes;
    }

    public Node[] AddWaypoints(Node[] nodes)
    {
        var newNodes = new List<Node>(nodes);
        foreach (var node in nodes)
        {
            foreach (var transition in node.Transitions)
            {
                var result = _router.QuerySingleDestination(
                    node.Position.Longitude, node.Position.Latitude,
                    transition.To.Position.Longitude, transition.To.Position.Latitude
                );

                var decodedPolyline = Polyline6ToPoints.DecodePolyline(result.Polyline);
                foreach (var waypoint in decodedPolyline)
                {
                    var position = new Position(waypoint.Longitude, waypoint.Latitude);
                    var waypointNode = new Node(position, []);
                    if (!newNodes.Any(n => n.Position.Equals(position)))
                        newNodes.Add(waypointNode);
                }
            }
        }
        return RemoveDuplicateTransitions(AddAllTransitions([.. newNodes]));
    }
}
