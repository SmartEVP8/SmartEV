using ProtoBuf;

namespace API.Models;

[ProtoContract]
public class EVChargerState
{
    [ProtoMember(1)]
    public int EvID { get; set; }

    [ProtoMember(2)]
    public float SoC { get; set; }

    [ProtoMember(3)]
    public float TargetSoC { get; set; }
}

[ProtoContract]
public class ChargerState
{
    [ProtoMember(1)]
    public bool IsActive { get; set; }

    [ProtoMember(2)]
    public float Utilization { get; set; }

    [ProtoMember(3)]
    public int ChargerId { get; set; }

    [ProtoMember(4)]
    public int QueueSize { get; set; }

    [ProtoMember(5)]
    public List<EVChargerState> EvsInQueue { get; set; } = new();
}

[ProtoContract]
public class EVOnRoute
{
    [ProtoMember(1)]
    public int EvId { get; set; }

    [ProtoMember(2)]
    public List<Position> Waypoints { get; set; } = new();
}

[ProtoContract]
public class RequestStationState
{
    [ProtoMember(1)]
    public List<ChargerState> States { get; set; } = new();

    [ProtoMember(2)]
    public List<EVOnRoute> EvsOnRoute { get; set; } = new();
}

[ProtoContract]
public class CostWeight
{
    [ProtoMember(1)]
    public int Id { get; set; }

    [ProtoMember(2)]
    public float UpdatedValue { get; set; }
}

[ProtoContract]
public class StationGenerationOptions
{
    [ProtoMember(1)]
    public float DualChargingPointProbability { get; set; }

    [ProtoMember(2)]
    public int TotalChargers { get; set; }
}

[ProtoContract]
public class Initialise
{
    [ProtoMember(1)]
    public List<CostWeight> CostWeights { get; set; } = new();

    [ProtoMember(2)]
    public uint MaximumEVs { get; set; }

    [ProtoMember(3)]
    public int Seed { get; set; }
}
